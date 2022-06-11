using System.DirectoryServices.Protocols;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace decovar.dev.Code
{
    public interface ISignInManager
    {
        Task<bool> SignIn(string username, string password);
        Task SignOut();
    }

    public class SignInManager : ISignInManager
    {
        private readonly ConfigurationAD _configurationAD;
        private readonly ILogger _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;
        //private readonly IDatabaseContext _databaseContext;

        public SignInManager(
            IOptions<ConfigurationAD> configurationAD,
            ILogger<SignInManager> logger,
            IHttpContextAccessor httpContextAccessor
            )
        {
            _configurationAD = configurationAD.Value;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
            //_databaseContext = new DatabaseContext(_configuration);
        }

        public async Task<bool> SignIn(string username, string password)
        {
            var adUser = new ADUser();

            var searchResults = General.SearchInAD(
                _configurationAD.LDAPserver,
                _configurationAD.Port,
                _configurationAD.Domain,
                username,
                password,
                $"CN=Users,{_configurationAD.LDAPQueryBase}",
                new StringBuilder("(&")
                    // Active Directory attributes
                    .Append("(objectCategory=person)")
                    .Append("(objectClass=user)")
                    // ---
                    // Synology DSM LDAP attribute
                    //.Append("(objectClass=person)")
                    // ---
                    .Append($"(memberOf={_configurationAD.Crew})")
                    // ---
                    // fails on Synology DSM LDAP, even though it should be supported
                    .Append("(!(userAccountControl:1.2.840.113556.1.4.803:=2))")
                    // ---
                    // Active Directory attribute
                    .Append($"(sAMAccountName={username})")
                    // ---
                    // Synology DSM LDAP attribute
                    //.Append($"(uid={username})")
                    // ---
                    .Append(")")
                    .ToString(),
                SearchScope.Subtree,
                new string[]
                {
                    "objectGUID",
                    "sAMAccountName",
                    "displayName",
                    "mail",
                    "whenCreated",
                    "memberof"
                }
            );

            var results = searchResults.Entries.Cast<SearchResultEntry>();
            if (results.Any())
            {
                var resultsEntry = results.First();
                adUser = new ADUser()
                {
                    objectGUID = new Guid((resultsEntry.Attributes["objectGUID"][0] as byte[])!),
                    sAMAccountName = resultsEntry.Attributes["sAMAccountName"][0].ToString()!,
                    displayName = resultsEntry.Attributes["displayName"][0].ToString()!,
                    mail = resultsEntry.Attributes["mail"][0].ToString()!,
                    whenCreated = DateTime.ParseExact(
                        resultsEntry.Attributes["whenCreated"][0].ToString()!,
                        "yyyyMMddHHmmss.0Z",
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                };
                var groups = resultsEntry.Attributes["memberof"];
                foreach(var g in groups)
                {
                    var groupNameBytes = g as byte[];
                    if (groupNameBytes != null)
                    {
                        adUser.memberOf.Add(Encoding.Default.GetString(groupNameBytes));
                    }
                }
            }
            else
            {
                _logger.LogWarning(
                    $"There is no such user in the [crew] group: {username}"
                );
                return false;
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, adUser.objectGUID.ToString()),
                new Claim(ClaimTypes.WindowsAccountName, adUser.sAMAccountName),
                new Claim(ClaimTypes.Name, adUser.displayName),
                new Claim(ClaimTypes.Email, adUser.mail),
                new Claim("whenCreated", adUser.whenCreated.ToString("yyyy-MM-dd"))
            };
            // perhaps it should add a role for every group, but we only need one for now
            if (adUser.memberOf.Contains(_configurationAD.Managers))
            {
                claims.Add(new Claim(ClaimTypes.Role, "managers"));
            }

            var identity = new ClaimsIdentity(
                claims,
                "LDAP", // what goes to User.Identity.AuthenticationType
                ClaimTypes.Name, // which claim is for storing user name in User.Identity.Name
                ClaimTypes.Role // which claim is for storing user roles, needed for User.IsInRole()
            );
            var principal = new ClaimsPrincipal(identity);

            if (_httpContextAccessor.HttpContext != null)
            {
                try
                {
                    await _httpContextAccessor.HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principal
                    );
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Signing in has failed. {ex.Message}");
                }
            }

            return false;
        }

        public async Task SignOut()
        {
            if (_httpContextAccessor.HttpContext != null)
            {
                await _httpContextAccessor.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme
                );
            }
            else
            {
                throw new Exception(
                    "For some reasons, HTTP context is null, signing out cannot be performed"
                );
            }
        }
    }
}
