using System.Text;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.VisualBasic.CompilerServices;
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
        if (context.Session.Get("CurrentSessionTracked") == null)
        {
            IncrementCookieValue(context, "totalSessions");
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

            // Update session actions
            TrackSessionActions(context, actionName);
            
            // Update total actions
            UpdateActionCount(context, "totalActions", actionName);
        }
    }

    private Dictionary<string, string> GetSessionActions(HttpContext context)
    {
        if(context.Session.Get("sessionActions") != null)
        {
            // Deserialize the session actions dictionary
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(
                context.Session.Get("sessionActions").ToString(),
                _jsonSettings
            ) ?? new Dictionary<string, string>();
        }
        
        return new Dictionary<string, string>();
    }

    private void TrackSessionActions(HttpContext context, string key)
    {
        var sessionActions = GetSessionActions(context);

        if (sessionActions.TryGetValue(key, out var currentValue))
        {
            // Convert the string and value of the session action into the corresponding key-value pair
            sessionActions[key] = (currentValue + 1).ToString();
        }
        else
        {
            sessionActions[key] = 1.ToString();
        }
        
        // Serialize the session actions dictionary
        var serializedSessionActions = JsonConvert.SerializeObject(sessionActions, _jsonSettings);
        
        // Save the session actions dictionary back to the session
        context.Session.Set("sessionActions", Encoding.UTF8.GetBytes(serializedSessionActions));
    }

    private void UpdateActionCount(
        HttpContext context,
        string actionType,
        string actionName)
    {
        var actionDict = GetActionDictionary(context, actionType);

        if (actionDict.TryGetValue(actionName, out var actionValues))
        {
            int currentControllerValue = int.Parse(actionDict[actionName]) + 1;
            actionDict[actionName] = currentControllerValue.ToString();
        }
        else
        {
            int currentControllerValue = 1;
            actionDict[actionName] = currentControllerValue.ToString();
        }

        SaveActionDictionary(context, actionType, actionDict);
    }

    private Dictionary<string, string> GetActionDictionary(
        HttpContext context,
        string actionType)
    {
        var cookieDict = GetCookieDictionary(context);

        if (cookieDict.TryGetValue(actionType, out var actionValue))
        {
            // Return the key for the given action type subdictionary
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(actionValue, _jsonSettings) ?? new Dictionary<string, string>();
        }

        return new Dictionary<string, string>();
    }

    private void SaveActionDictionary(
        HttpContext context,
        string actionType,
        Dictionary<string, string> actionDict)
    {
        var actionSerialized = JsonConvert.SerializeObject(actionDict);
        UpdateCookie(context, actionType, actionSerialized);
    }

    private void IncrementCookieValue(
        HttpContext context,
        string key)
    {
        var cookieDict = GetCookieDictionary(context);
        int currentValue = 1;

        if (cookieDict.TryGetValue(key, out var value))
        {
            currentValue = int.Parse(value) + 1;
        }

        string newValue = currentValue.ToString();
        UpdateCookie(context, key, newValue);
    }

    private Dictionary<string, string> GetCookieDictionary(HttpContext context)
    {
        // If a cookie exists already for the given visitor...
        if (context.Request.Cookies.TryGetValue(CookieName, out _))
        {
            // Then we'll try to deserialize it back into a dictionary - however, it might
            // be null
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(context.Request.Cookies[CookieName], _jsonSettings) ?? new Dictionary<string, string>();
        }
        
        return new Dictionary<string, string>();
    }

    private void UpdateCookie(HttpContext context, string key, string value)
    {
        // First we get the original cookie values
        var mergedExistingAndNewCookies = GetCookieDictionary(context);
        
        // Next we update the value for the given key
        mergedExistingAndNewCookies[key] = value;
        
        // Finally, we save the cookie with the new dictionary
        SaveCookie(context, mergedExistingAndNewCookies);
    }
    
    private void SaveCookie(
        HttpContext context,
        Dictionary<string, string> cookieDict)
    {
        // We can now delete the existing cookie
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

    public int? GetActionCount(HttpContext context, string pageName)
    {
        var actionDictionary = GetActionDictionary(context, "totalActions");

        if (actionDictionary.TryGetValue(pageName, out var numberOfVisits))
        {
            return int.Parse(actionDictionary[pageName]);
        }
        else
        {
            return null;
        }
    }
}