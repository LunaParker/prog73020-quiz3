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
        TrackSessionCount(context);
        await _next(context); // Let the action execute first
        TrackControllerAction(context);
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
        if (context.Request.Cookies.TryGetValue(CookieName, out var cookieValue))
        {
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(cookieValue, _jsonSettings);
        }
        
        return new Dictionary<string, string>();
    }

    private void SaveCookie(
        HttpContext context,
        Dictionary<string, string> cookieDict)
    {
        var serialized = JsonConvert.SerializeObject(cookieDict, _jsonSettings);
        context.Response.Cookies.Append(
            CookieName,
            serialized,
            new CookieOptions { Expires = DateTime.Now.AddYears(2) }
        );
    }
}