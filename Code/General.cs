using System.DirectoryServices.Protocols;
using System.Text;

namespace decovar.dev.Code;

public static class General
{
    private static NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

    public static void LogException(
        Exception ex,
        IConfiguration configuration
        )
    {
        string? st = string.Empty;
        if (ex.StackTrace != null)
        {
            using (var reader = new StringReader(ex.StackTrace))
            {
                st = reader.ReadLine();
                if (!string.IsNullOrEmpty(st)) { st = $"{Environment.NewLine}{st}"; }
            }
        }
        // log that exception
        _logger.Error($"{ex.Message}{st}");
    }

    // based on https://www.codemag.com/article/1312041/Using-Active-Directory-in-.NET
    // won't work on non-Windows hosts, as it requires System.DirectoryServices
    /*
    public void SearchInADwindowsOnly(
        string ldapServer,
        string username,
        string password,
        string filter // "(&(objectCategory=User)(objectClass=person))"
        )
    {
        SearchResultCollection results;
        DirectorySearcher ds = null;
        DirectoryEntry de = new DirectoryEntry($"LDAP://{ldapServer}");
        de.Username = username;
        de.Password = password;

        ds = new DirectorySearcher(de);
        ds.Filter = filter;

        results = ds.FindAll();

        foreach (SearchResult sr in results)
        {
            // using the index zero (0) is required
            Console.WriteLine(sr.Properties["name"][0].ToString());
        }
    }
    */

    // based on https://github.com/dotnet/runtime/issues/36947#issuecomment-744046087
    public static SearchResponse SearchInAD(
        string ldapServer,
        int ldapPort,
        string domainForAD,
        string username,
        string password,
        string targetOU,
        string query,
        SearchScope scope,
        params string[] attributeList
        )
    {
        //string ldapServer = $"{subdomain}.{domain}.{zone}";
        //_logger.Debug($"Using LDAP server: {ldapServer}");

        // https://github.com/dotnet/runtime/issues/63759#issuecomment-1019318988
        // on Windows the authentication type is Negotiate, so there is no need to prepend
        // AD user login with domain. On other platforms at the moment only Basic authentication
        // is supported
        var authType = AuthType.Negotiate;
        if (!OperatingSystem.IsWindows())
        {
            authType = AuthType.Basic;
            username = OperatingSystem.IsWindows()
                ? username
                // this might require modification to the actual AD domain value
                : $"{domainForAD}\\{username}";
        }

        // depending on LDAP server, username might require some proper wrapping
        // instead(!) of just prepending username with domain
        //username = $"uid={username},cn=users,dc=subdomain,dc=domain,dc=zone";

        //var connection = new LdapConnection(ldapServer)
        var connection = new LdapConnection(
            new LdapDirectoryIdentifier(ldapServer, ldapPort)
            )
        {
            AuthType = authType,
            Credential = new(username, password)
        };
        // the default one is v2 (at least in that version), and it is unknown if v3
        // is actually needed, but at least Synology LDAP works only with v3,
        // and since our Exchange doesn't complain, let it be v3
        connection.SessionOptions.ProtocolVersion = 3;
        // doesn't work on non-Windows hosts, so you can't connect via LDAPS (636 port)
        //connection.SessionOptions.SecureSocketLayer = true;

        connection.Bind();

        _logger.Debug($"Searching scope: [{scope}], target: [{targetOU}], query: [{query}]");
        var request = new SearchRequest(targetOU, query, scope, attributeList);

        return (SearchResponse)connection.SendRequest(request);
    }
}
