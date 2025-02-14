using System.Text;
using Microsoft.AspNetCore.Mvc.Controllers;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

public class PageCountMiddleware
{
    private readonly RequestDelegate _next;
    private const string CookieName = "UserActions";

    private readonly JsonSerializerSettings _jsonSettings = new()
    {
        Formatting = Formatting.None,
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };

    public PageCountMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Register before next() to execute before response starts
        context.Response.OnStarting(() => 
        {
            TrackSessionCount(context);
            TrackControllerAction(context);
            return Task.CompletedTask;
        });

        await _next(context);
    }

    private void TrackSessionCount(HttpContext context)
    {
        if (!context.Session.TryGetValue("CurrentSessionTracked", out _))
        {
            UpdateCookieValue(context, "SessionCount", current => current + 1);
            ResetSessionActions(context);
            context.Session.Set("CurrentSessionTracked", Encoding.UTF8.GetBytes("true"));
        }
    }

    private void TrackControllerAction(HttpContext context)
    {
        var actionDescriptor = context.GetEndpoint()?.Metadata
            .GetMetadata<ControllerActionDescriptor>();

        if (actionDescriptor != null)
        {
            var actionName = $"{actionDescriptor.ControllerName}/{actionDescriptor.ActionName}";

            // Update total actions
            UpdateActionCount(context, "TotalActions", actionName);

            // Update session actions
            UpdateActionCount(context, "SessionActions", actionName);
        }
    }

    private void UpdateActionCount(
        HttpContext context,
        string actionType,
        string actionName)
    {
        var actionDict = GetActionDictionary(context, actionType);

        if (actionDict.ContainsKey(actionName))
        {
            actionDict[actionName]++;
        }
        else
        {
            actionDict[actionName] = 1;
        }

        SaveActionDictionary(context, actionType, actionDict);
    }

    private Dictionary<string, int> GetActionDictionary(
        HttpContext context,
        string actionType)
    {
        var cookieDict = GetCookieDictionary(context);

        if (cookieDict.TryGetValue(actionType, out var json))
        {
            return JsonConvert.DeserializeObject<Dictionary<string, int>>(json)
                   ?? new Dictionary<string, int>();
        }

        return new Dictionary<string, int>();
    }

    private void SaveActionDictionary(
        HttpContext context,
        string actionType,
        Dictionary<string, int> actionDict)
    {
        var cookieDict = GetCookieDictionary(context);
        cookieDict[actionType] = JsonConvert.SerializeObject(actionDict);
        SaveCookie(context, cookieDict);
    }

    private void ResetSessionActions(HttpContext context)
    {
        var cookieDict = GetCookieDictionary(context);
        cookieDict["SessionActions"] = "{}";
        SaveCookie(context, cookieDict);
    }

    private void UpdateCookieValue(
        HttpContext context,
        string key,
        Func<int, int> updateFn)
    {
        var cookieDict = GetCookieDictionary(context);
        int currentValue = 0;

        if (cookieDict.ContainsKey(key))
        {
            currentValue = int.Parse(cookieDict[key]);
        }

        cookieDict[key] = updateFn(currentValue).ToString();
        SaveCookie(context, cookieDict);
    }

    private Dictionary<string, string> GetCookieDictionary(HttpContext context)
    {
        // If a cookie exists already for the given visitor...
        if (context.Request.Cookies.TryGetValue(CookieName, out var cookieValue))
        {
            // Then we'll try to deserialize it back into a dictionary - however, it might
            // be null
            Dictionary<string, string> deserializedCookie = JsonConvert.DeserializeObject<Dictionary<string, string>>(cookieValue, _jsonSettings);
            
            // If it's null, we'll return an empty dictionary, otherwise, we'll return
            // a dictionary with the deserialized value
            return deserializedCookie ?? new Dictionary<string, string>();
        }
        
        return new Dictionary<string, string>();
    }

    private void SaveCookie(
        HttpContext context,
        Dictionary<string, string> cookieDict)
    {
        // First, delete the existing cookie
        context.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            Path = "/",
            SameSite = SameSiteMode.Lax,
            IsEssential = true
        });
        
        // Then, serialize the dictionary into a JSON format (which can be easily
        // saved as a string, which is the format cookies use)
        var serialized = JsonConvert.SerializeObject(cookieDict, _jsonSettings);
        
        // Finally, save the new cookie with the updated JSON string
        context.Response.Cookies.Append(
            CookieName,
            serialized,
            new CookieOptions
            {
                Expires = DateTime.Now.AddYears(2),
                Path = "/",
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            }
        );
    }
}