using System.Collections;
using System.DirectoryServices.Protocols;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using decovar.dev.Code;

namespace decovar.dev.Controllers;

[Authorize(Roles = "managers")]
[Route("admin")]
public class AdminController : Controller
{
    private readonly ConfigurationAD _configurationAD;
    private readonly ILogger _logger;

    public AdminController(
        IOptions<ConfigurationAD> configurationAD,
        ILogger<AdminController> logger
        )
    {
        _configurationAD = configurationAD.Value;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var adProperties = new Dictionary<string, string>();
        try
        {
            // Search the base OU to get attributes like defaultNamingContext and supportedCapabilities.
            // If supportedCapabilities contains 1.2.840.113556.1.4.800 then we know it's an Active Directory per
            // https://www.alvestrand.no/objectid/1.2.840.113556.1.4.800.html.
            var searchResults = General.SearchInAD(
                _configurationAD.LDAPserver,
                _configurationAD.Port,
                _configurationAD.Domain,
                _configurationAD.Username,
                _configurationAD.Password,
                string.Empty,
                "objectClass=*",
                SearchScope.Base,
                "defaultNamingContext",
                "dnsHostName",
                "supportedCapabilities"
            );

            foreach (SearchResultEntry searchEntry in searchResults.Entries)//response.Entries.Cast<SearchResultEntry>().Take(EntryDisplayLimit))
            {
                if (!string.IsNullOrEmpty(searchEntry.DistinguishedName))
                {
                    Console.WriteLine($"Distinguished name: {searchEntry.DistinguishedName}");
                }
                foreach (DictionaryEntry attributeEntry in searchEntry.Attributes
                    .Cast<DictionaryEntry>().OrderBy(a => a.Key))
                {
                    DirectoryAttribute attribute;
                    if (attributeEntry.Value != null)
                    {
                        attribute = (DirectoryAttribute)attributeEntry.Value;
                    }
                    else { continue; }

                    IReadOnlyList<object> values = attribute.Cast<object>()
                        .Select(value => value is byte[] bytes
                            ? (bytes.Length > 0 && !bytes.Any(b => b == 0)
                                ? Encoding.UTF8.GetString(bytes)
                                : ("0x" + string.Concat(bytes.Select(b => b.ToString("X2")))))
                            : value)
                        //.Distinct()
                        .ToList();
                    //Console.WriteLine($"\t{attribute.Name} = {string.Join(" | ", values)}");
                    adProperties.Add(attribute.Name, string.Join(" | ", values));
                }
            }

            // On Windows we can do user searches from the defaultNamingContext's container.
            // On Linux we have to target the "CN=Users" container, or the search throws: DirectoryOperationException: An operation error occurred.
            // Maybe on Linux SearchScope.Subtree is behaving like SearchScope.OneLevel?
            // General.SearchInAD(
            //     _configurationAD.LDAPserver,
            //     _configurationAD.Username,
            //     _configurationAD.Password,
            //     //(OperatingSystem.IsWindows() ? string.Empty : "CN=Users,") + _DefaultNamingContext,
            //     $"CN=Users,{_configurationAD.LDAPconnection}",
            //     "(&(objectCategory=Person)(objectClass=user)(displayName=*vasya*))",
            //     SearchScope.Subtree,
            //     "cn",
            //     "sAMAccountName",
            //     "objectSid"
            // );
        }
        catch (LdapException ex)
        {
            Console.WriteLine(ex.ErrorCode);
            Console.WriteLine(ex.ServerErrorMessage);
            Console.WriteLine(ex.ToString());
        }
        catch (DirectoryOperationException ex)
        {
            Console.WriteLine(ex.Response);
            Console.WriteLine(ex.ToString());
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }

        return View(adProperties);
    }
}
