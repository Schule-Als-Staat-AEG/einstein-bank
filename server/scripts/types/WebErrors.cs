namespace Max35050;

public class Unauthorized : IResult
{
    public object Content { get; set; } = new { message = "Unauthorized" };
    public Unauthorized() { }
    public Unauthorized(object content)
    {
        Content = content;
    }
    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 401;
        httpContext.Response.ContentType = "application/json";
        return httpContext.Response.WriteAsJsonAsync(Content);
    }
}

public class Forbidden : IResult
{
    public object Content { get; set; } = new { message = "Forbidden" };
    public Forbidden() { }
    public Forbidden(object content)
    {
        Content = content;
    }
    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 403;
        httpContext.Response.ContentType = "application/json";
        return httpContext.Response.WriteAsJsonAsync(Content);
    }
}
public class RateLimited : IResult
{
    public object Content { get; set; } = new { message = "Too Many Requests" };
    public RateLimited() { }
    public RateLimited(object content)
    {
        Content = content;
    }
    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = 429;
        httpContext.Response.ContentType = "application/json";
        return httpContext.Response.WriteAsJsonAsync(Content);
    }
}