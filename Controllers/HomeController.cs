using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using decovar.dev.Code;
using decovar.dev.Models;

namespace decovar.dev.Controllers;

[Route("/")]
public class HomeController : Controller
{
    private readonly ConfigurationAD _configurationAD;
    private readonly ILogger _logger;

    public HomeController(
        IOptions<ConfigurationAD> configurationAD,
        ILogger<HomeController> logger
        )
    {
        _configurationAD = configurationAD.Value;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var listOfUsers = new List<ADUser>();

        var attributesToQuery = new string[]
        {
            "objectGUID",
            "sAMAccountName",
            "displayName",
            "mail",
            "whenCreated"
        };
        var searchResults = General.SearchInAD(
            _configurationAD.LDAPserver,
            _configurationAD.Port,
            _configurationAD.Domain,
            _configurationAD.Username,
            _configurationAD.Password,
            $"CN=Users,{_configurationAD.LDAPQueryBase}",
            new StringBuilder("(&")
                .Append("(objectCategory=person)")
                .Append("(objectClass=user)")
                .Append($"(memberOf={_configurationAD.Crew})")
                .Append("(!(userAccountControl:1.2.840.113556.1.4.803:=2))")
                .Append(")")
                .ToString(),
            SearchScope.Subtree,
            attributesToQuery
        );

        foreach (var searchEntry in searchResults.Entries.Cast<SearchResultEntry>())
        {
            // you can iterate results for debugging purposes
            // all that [0] assumes that each attribute has only one value
            /*
            foreach (var attr in attributesToQuery)
            {
                if (searchEntry.Attributes[attr][0].GetType() != typeof(System.Byte[]))
                {
                    var attrValue = searchEntry.Attributes[attr][0].ToString();
                }
                else // must be bytes then
                {
                    var attrValue = searchEntry.Attributes[attr][0] as byte[];
                    if (attrValue != null)
                    {
                        // can't get a normal string out of it like this
                        var gdString = Encoding.Default.GetString(attrValue);
                        // only can compose a bare string representation of those bytes
                        var gdStringBytes = string.Concat(attrValue!.Select(b => b.ToString("X2")));
                    }
                }
            }
            */

            // can get normal strings out of those bytes only with special constructors for each kind
            /*
            var bytes = searchEntry.Attributes["objectGUID"][0] as byte[]; // ["objectSid"]
            if (bytes != null)
            {
                // bare string of bytes, which doesn't make much of a sense, if any
                var bytesAsString = string.Concat(bytes!.Select(b => b.ToString("X2")));
                // if it's objectGUID, then use Guid(). If you pass objectSid here,
                // then it will fail with "Byte array for Guid must be exactly 16 bytes long"
                var guid = new Guid(bytes);
                // only if it's objectSid, then use SecurityIdentifier(). If you pass objectGUID here,
                // then it will fail with "SIDs with revision other than '1' are not supported"
                // and also it seems to work only on Windows
                //var securityIdentifier = new SecurityIdentifier(bytes, 0);
            }
            */

            listOfUsers.Add(
                new ADUser()
                {
                    objectGUID = new Guid((searchEntry.Attributes["objectGUID"][0] as byte[])!),
                    sAMAccountName = searchEntry.Attributes["sAMAccountName"][0].ToString()!,
                    displayName = searchEntry.Attributes["displayName"][0].ToString()!,
                    mail = searchEntry.Attributes["mail"][0].ToString()!,
                    whenCreated = DateTime.ParseExact(
                        searchEntry.Attributes["whenCreated"][0].ToString()!,
                        "yyyyMMddHHmmss.0Z",
                        System.Globalization.CultureInfo.InvariantCulture
                    )
                }
            );
        }

        return View(listOfUsers);
    }

    [AllowAnonymous]
    [Route("error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(
            new ErrorViewModel {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            }
        );
    }

    [AllowAnonymous]
    [Route("error/{statusCode}")]
    public IActionResult Error(int statusCode)
    {
        return View("StatusCode", statusCode);
    }
}
