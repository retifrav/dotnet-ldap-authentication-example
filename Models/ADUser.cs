namespace decovar.dev;

public class ADUser
{
    public Guid objectGUID { get; set; }

    public string sAMAccountName { get; set; } = string.Empty;
    public string displayName { get; set; } = string.Empty;
    public string mail { get; set; } = string.Empty;

    public DateTime whenCreated { get; set; }

    public List<string> memberOf { get; set; } = new List<string>();
}
