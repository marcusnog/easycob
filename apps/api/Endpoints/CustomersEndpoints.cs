using EasyCob.Core.Data;
using EasyCob.Core.Modules.Customers;
using EasyCob.Core.Modules.Billing;
using Microsoft.EntityFrameworkCore;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace EasyCob.Api.Endpoints;

internal static class CustomersEndpoints
{
    public static void MapCustomers(this WebApplication app)
    {
        var group = app.MapGroup("/customers").RequireAuthorization();

        group.MapGet("/", async (string? search, int? page, EasyCobDbContext db, CancellationToken ct) =>
        {
            var query = db.Customers.Where(x => x.ArchivedAt == null);
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(x => EF.Functions.ILike(x.Name, $"%{search.Trim()}%") || x.Document == search.Trim());
            return await query.OrderBy(x => x.Name).Skip((Math.Max(page ?? 1, 1) - 1) * 50).Take(50)
                .Select(x => new { x.Id, x.Name, x.Document, x.CreatedAt }).ToListAsync(ct);
        });

        group.MapGet("/{id:guid}", async (Guid id, EasyCobDbContext db, CancellationToken ct) =>
        {
            var customer = await db.Customers.Where(x => x.Id == id && x.ArchivedAt == null)
                .Select(x => new { x.Id, x.Name, x.Document, Contacts = db.Contacts.Where(c => c.CustomerId == x.Id).ToArray() })
                .SingleOrDefaultAsync(ct);
            return customer is null ? Results.NotFound() : Results.Ok(customer);
        });

        group.MapPost("/", async (CustomerRequest request, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return Invalid("name", "Nome é obrigatório.");
            var document = EmptyToNull(request.Document);
            if (document is not null && await db.Customers.AnyAsync(x => x.Document == document, ct))
                return Results.Conflict(new { error = "Já existe cliente com este documento." });

            var customer = new Customer { Name = request.Name.Trim(), Document = document };
            db.Customers.Add(customer);
            db.Audit(http.User, "customer.created", nameof(Customer), customer.Id);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/customers/{customer.Id}", new { customer.Id });
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin", "Finance", "Collector"));

        group.MapPut("/{id:guid}", async (Guid id, CustomerRequest request, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name)) return Invalid("name", "Nome é obrigatório.");
            var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct);
            if (customer is null) return Results.NotFound();
            var document = EmptyToNull(request.Document);
            if (document is not null && await db.Customers.AnyAsync(x => x.Id != id && x.Document == document, ct))
                return Results.Conflict(new { error = "Já existe cliente com este documento." });
            customer.Name = request.Name.Trim();
            customer.Document = document;
            db.Audit(http.User, "customer.updated", nameof(Customer), id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin", "Finance", "Collector"));

        group.MapDelete("/{id:guid}", async (Guid id, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id && x.ArchivedAt == null, ct);
            if (customer is null) return Results.NotFound();
            customer.ArchivedAt = DateTimeOffset.UtcNow;
            db.Audit(http.User, "customer.archived", nameof(Customer), id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));

        group.MapPost("/{customerId:guid}/contacts", async (Guid customerId, ContactRequest request, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            if (!await db.Customers.AnyAsync(x => x.Id == customerId && x.ArchivedAt == null, ct)) return Results.NotFound();
            var phone = NormalizePhone(request.Phone);
            var email = EmptyToNull(request.Email);
            if (!string.IsNullOrWhiteSpace(request.Phone) && phone is null) return Invalid("phone", "Telefone deve conter de 10 a 15 dígitos.");
            if (phone is null && email is null) return Invalid("contact", "Informe telefone ou e-mail.");
            if (request.WhatsAppOptIn && phone is null) return Invalid("phone", "Telefone é obrigatório para consentimento WhatsApp.");
            var contact = new Contact
            {
                CustomerId = customerId,
                Phone = phone,
                Email = email,
                WhatsAppOptIn = request.WhatsAppOptIn,
                ConsentAt = request.WhatsAppOptIn ? DateTimeOffset.UtcNow : null
            };
            db.Contacts.Add(contact);
            db.Audit(http.User, "contact.created", nameof(Contact), contact.Id);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/customers/{customerId}/contacts/{contact.Id}", new { contact.Id });
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin", "Collector"));

        group.MapPut("/{customerId:guid}/contacts/{id:guid}/consent", async (Guid customerId, Guid id, ConsentRequest request, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            var contact = await db.Contacts.SingleOrDefaultAsync(x => x.Id == id && x.CustomerId == customerId, ct);
            if (contact is null) return Results.NotFound();
            if (request.OptIn && string.IsNullOrWhiteSpace(contact.Phone)) return Invalid("phone", "Contato sem telefone.");
            contact.WhatsAppOptIn = request.OptIn;
            contact.ConsentAt = request.OptIn ? DateTimeOffset.UtcNow : contact.ConsentAt;
            contact.OptOutAt = request.OptIn ? null : DateTimeOffset.UtcNow;
            db.Audit(http.User, request.OptIn ? "contact.opted-in" : "contact.opted-out", nameof(Contact), id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin", "Collector"));

        group.MapPost("/import", async (IFormFile file, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            if (file.Length is 0 or > 10_485_760) return Invalid("file", "Arquivo CSV deve ter até 10 MiB.");
            var existing = (await db.Customers.Where(x => x.Document != null).Select(x => x.Document!).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var created = 0;
            var skipped = 0;
            try
            {
                await using var stream = file.OpenReadStream();
                using var text = new StreamReader(stream);
                using var csv = new CsvReader(text, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    PrepareHeaderForMatch = args => args.Header.Replace("_", "").Replace(" ", "").Trim().ToLowerInvariant(),
                    MissingFieldFound = null,
                    HeaderValidated = null
                });
                await foreach (var row in csv.GetRecordsAsync<CustomerCsvRow>(ct))
                {
                    if (string.IsNullOrWhiteSpace(row.Name)) return Invalid("csv", $"Nome ausente na linha {csv.Parser.Row}.");
                    var document = EmptyToNull(row.Document);
                    if (document is not null && !existing.Add(document)) { skipped++; continue; }
                    var customer = new Customer { Name = row.Name.Trim(), Document = document };
                    db.Customers.Add(customer);
                    var phone = NormalizePhone(row.Phone);
                    var email = EmptyToNull(row.Email);
                    if (!string.IsNullOrWhiteSpace(row.Phone) && phone is null) return Invalid("csv", $"Telefone inválido na linha {csv.Parser.Row}.");
                    if (row.WhatsAppOptIn && phone is null) return Invalid("csv", $"Opt-in sem telefone na linha {csv.Parser.Row}.");
                    if (phone is not null || email is not null)
                        db.Contacts.Add(new Contact
                        {
                            CustomerId = customer.Id,
                            Phone = phone,
                            Email = email,
                            WhatsAppOptIn = row.WhatsAppOptIn && phone is not null,
                            ConsentAt = row.WhatsAppOptIn && phone is not null ? DateTimeOffset.UtcNow : null
                        });
                    created++;
                }
            }
            catch (CsvHelperException) { return Invalid("csv", "CSV inválido; confira cabeçalhos e tipos dos campos."); }
            db.Audit(http.User, "customers.imported", nameof(Customer), Guid.Empty);
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { created, skipped });
        }).DisableAntiforgery().RequireAuthorization(policy => policy.RequireRole("Owner", "Admin", "Finance"));

        group.MapGet("/{id:guid}/export", async (Guid id, EasyCobDbContext db, CancellationToken ct) =>
        {
            var customer = await db.Customers.Where(x => x.Id == id).Select(x => new
            {
                x.Id,
                x.Name,
                x.Document,
                x.CreatedAt,
                x.ArchivedAt,
                Contacts = db.Contacts.Where(c => c.CustomerId == id).Select(c => new { c.Email, c.Phone, c.WhatsAppOptIn, c.ConsentAt, c.OptOutAt }).ToArray(),
                Charges = db.Charges.Where(c => c.CustomerId == id).Select(c => new
                {
                    c.Id,
                    c.Description,
                    c.Amount,
                    c.DueDate,
                    c.Status,
                    Payments = db.Payments.Where(p => p.ChargeId == c.Id).Select(p => new { p.Amount, p.PaidAt, p.ExternalId }).ToArray()
                }).ToArray()
            }).SingleOrDefaultAsync(ct);
            return customer is null ? Results.NotFound() : Results.Ok(customer);
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));

        group.MapPost("/{id:guid}/anonymize", async (Guid id, HttpContext http, EasyCobDbContext db, CancellationToken ct) =>
        {
            var customer = await db.Customers.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (customer is null) return Results.NotFound();
            if (await db.Charges.AnyAsync(x => x.CustomerId == id && x.Status != ChargeStatus.Paid && x.Status != ChargeStatus.Cancelled, ct))
                return Results.Conflict(new { error = "Cliente possui cobrança em aberto." });
            customer.Name = "Titular removido";
            customer.Document = null;
            customer.ArchivedAt = DateTimeOffset.UtcNow;
            foreach (var contact in await db.Contacts.Where(x => x.CustomerId == id).ToListAsync(ct))
            {
                contact.Email = null;
                contact.Phone = null;
                contact.WhatsAppOptIn = false;
                contact.OptOutAt = DateTimeOffset.UtcNow;
            }
            db.Audit(http.User, "customer.anonymized", nameof(Customer), id);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Owner", "Admin"));
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length is >= 10 and <= 15 ? digits : null;
    }
    private static IResult Invalid(string key, string message) => Results.ValidationProblem(new Dictionary<string, string[]> { [key] = [message] });
}

internal sealed record CustomerRequest(string Name, string? Document);
internal sealed record ContactRequest(string? Phone, string? Email, bool WhatsAppOptIn);
internal sealed record ConsentRequest(bool OptIn);
internal sealed class CustomerCsvRow
{
    public string Name { get; set; } = string.Empty;
    public string? Document { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool WhatsAppOptIn { get; set; }
}
