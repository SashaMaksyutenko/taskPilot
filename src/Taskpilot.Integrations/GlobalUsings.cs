// This is a plain class library (Microsoft.NET.Sdk), whose implicit usings do NOT include
// Microsoft.Extensions.Logging the way the ASP.NET Web SDK did in the API. The senders use
// ILogger<T>, so bring the namespace in globally.
global using Microsoft.Extensions.Logging;
