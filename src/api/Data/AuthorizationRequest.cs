namespace Microsoft.UsEduCsu.Saas;

// TODO: Convert from camelCase to ProperCase during serialization
public class AuthorizationRequest
{
	public string identity { get; set; }
	public string role { get; set; }

	public string Identity
	{
		get => identity;
	}
	public string Role
	{
		get => role;
	}
}