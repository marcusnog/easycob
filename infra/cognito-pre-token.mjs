export const handler = async (event) => {
  const tenantId = event.request.userAttributes["custom:tenant_id"];
  if (!tenantId) throw new Error("Usuário sem custom:tenant_id");
  const claims = { tenant_id: tenantId };
  event.response = {
    claimsAndScopeOverrideDetails: {
      idTokenGeneration: { claimsToAddOrOverride: claims },
      accessTokenGeneration: { claimsToAddOrOverride: claims },
      groupOverrideDetails: event.request.groupConfiguration,
    },
  };
  return event;
};
