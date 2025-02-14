using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace UserActionTrackingApp.Models;

public class PageSessionViewModel
{
    private const string CookieName = "UserActions";
    private readonly HttpContext _context;
    
    private readonly JsonSerializerSettings _jsonSettings = new()
    {
        Formatting = Formatting.None,
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };
    
    public PageSessionViewModel(HttpContext context)
    {
        _context = context;
    }

    public Dictionary<string, string>? GetUserActionTrackingDictionary()
    {
        if (_context.Request.Cookies.TryGetValue(CookieName, out var cookieValue))
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(cookieValue, _jsonSettings);
        }
        
        return null;
    }
}