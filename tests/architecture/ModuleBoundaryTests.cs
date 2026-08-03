using EasyCob.Core.Modules.Customers;
using EasyCob.Core.Tenancy;

namespace EasyCob.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    [Fact]
    public void BusinessEntities_ExceptTenant_ImplementTenantEntity()
    {
        var missing = typeof(Customer).Assembly.GetTypes()
            .Where(x => x.IsClass && !x.IsAbstract && x.DeclaringType is null && x.Namespace?.StartsWith("EasyCob.Core.Modules.") == true)
            .Where(x => x.FullName != "EasyCob.Core.Modules.Tenancy.Tenant")
            .Where(x => !typeof(ITenantEntity).IsAssignableFrom(x))
            .Select(x => x.FullName)
            .ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void BusinessModules_DoNotReferenceEachOther()
    {
        var moduleTypes = typeof(Customer).Assembly.GetTypes()
            .Where(x => x.Namespace?.StartsWith("EasyCob.Core.Modules.") == true);

        foreach (var type in moduleTypes)
        {
            var ownModule = type.Namespace!.Split('.')[3];
            var foreignModules = type.GetProperties()
                .Select(x => x.PropertyType.Namespace)
                .Where(x => x?.StartsWith("EasyCob.Core.Modules.") == true)
                .Select(x => x!.Split('.')[3])
                .Where(x => x != ownModule);
            Assert.Empty(foreignModules);
        }
    }
}
