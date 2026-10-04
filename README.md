# ISRORBilling
- [ISRORBilling](#isrorbilling)
    * [Appsettings layout](#appsettings-layout)
    * [About services](#about-services)
        + [Authentication Service](#authentication-service)
        + [Notification Service](#notification-service)
        + [Nation Ping Service](#nation-ping-service)

<small><i><a href='http://ecotrust-canada.github.io/markdown-toc/'>Table of contents generated with markdown-toc</a></i></small>


As this is an educational project, the idea is to use it to learn new ways of working and help each other improve.


This tool handles the login from users into ISROR files; It has been designed extensible, so you can create your own login flow if you want to.

## Appsettings layout
Appsettings is where you can configure the tool's behavior, you can override appsettings values depending on the environment you are running (`Development`, `Staging`, `Production`, etc...)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "Kestrel": {
    "EndPoints": {
      "Http": {
        "Url": "http://0.0.0.0:18080" // 👈 Listening address. Note the port 18080 to avoid collisions.
      }
    }
  },
  "AuthService": "Simple", // 👈 Supported types: Simple, Full, Bypass, Nemo.
  "DbConfig": {
    "AccountDB": "Data Source=.\\;TrustServerCertificate=True;Initial Catalog=SILKROAD_R_ACCOUNT;User ID=sa;Password=1;",
    "JoymaxPortalDB": "Data Source=.\\;TrustServerCertificate=True;Initial Catalog=GB_JoymaxPortal;User ID=sa;Password=1;"
  },
  "NotificationService": {
    "Type": "Email" // 👈 Email (recommended) or Ferre
  },
  "EmailService": {
    "From": "yourEmail",
    "FromFriendlyName": "YourServerName??",
    "SmtpServer": "smtp.gmail.com",
    "Port": 465,
    "Username": "FOLLOW https://code-maze.com/aspnetcore-send-email/",
    "Password": "FOLLOW https://code-maze.com/aspnetcore-send-email/",
    "SkipTokenValidation": false,
    "Templates": { ... } // 👈 Optional: customize the email texts, see the Notification Service README
  },
  "LoginThrottle": {
    "Enabled": true,
    "WindowMinutes": 15,       // 👈 Failed logins are counted per window
    "MaxFailuresPerUser": 10,  // 👈 Then that user id gets "blocked" until the window ends (0 = no limit)
    "MaxFailuresPerIp": 30     // 👈 Then that player IP gets "blocked IP" until the window ends (0 = no limit)
  },
  "Metrics": {
    "Enabled": true // 👈 Prometheus metrics on /metrics, see below
  },
  "NationPingService": {
    "ListenAddress": "0.0.0.0",
    "ListenPort": 12989
  },
  "ServiceCompany": 11,
  "RequestTimeoutSeconds": 60, // 👈 Login requests older than this are rejected (replay protection). GatewayServer and billing clocks must be in sync.
  "PortalCGIAgentHeader": "Portal_CGI_Agent", // 👈 Only filters casual browsing; it's trivial to fake, the SaltKey is the real protection.
  "SaltKey": "eset5ag.nsy-g6ky5.mp",  // 👈 Used to validate payloads in the auth services. It must match the GatewayServer hardcoded value!
  "AllowedHosts": "*" // 👈 learn more: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/host-filtering?view=aspnetcore-6.0
}
```

You can see more details on [Application and Host Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?source=recommendations&view=aspnetcore-7.0#application-and-host-configuration) on Microsoft's documentation.

But in a nutshell, 
1. you can create `appsettings.myserver.json` and override some configs there.
2. On your environment variables you set `ASPNETCORE_ENVIRONMENT=myserver` 

The end result will be that appsettings.json will be merged internally with `appsettings.myserver.json`. 

Useful for setting certain credentials that you don't want committed to git by accident.

## Brute-force protection
Failed logins (wrong password, unknown user) are counted per user id and per player IP, using the IP that the GatewayServer sends in the signed request. Billing's HTTP caller is always the GatewayServer, so the HTTP IP would be useless here. When a limit is reached, further logins for that user or IP are rejected (`BlockedJid` / `BlockedIp`) until the window ends. A successful login resets the user's counter. Counters live in memory, so they reset when the billing restarts. Configure it under `LoginThrottle`.

## Health check
`GET /health` returns `Healthy` (HTTP 200) when the databases used by the selected services are reachable, and `Unhealthy` (HTTP 503) otherwise. It doesn't require the portal User-Agent, so you can point a monitoring tool (Uptime Kuma, a load balancer, etc.) at it.

## Metrics
`GET /metrics` exposes [Prometheus](https://prometheus.io/) metrics, so you can graph the billing in Grafana. Like `/health`, it doesn't require the portal User-Agent. Set `Metrics:Enabled` to `false` to turn it off.

| Metric | What it counts |
|---|---|
| `billing_logins_total{result}` | Login requests by result: `success`, `wrong_password`, `user_not_found`, `error`, `blocked_user`, `blocked_ip`, `expired_request`, `invalid_signature`, `malformed`, `other` |
| `billing_login_duration_seconds` | How long checking a login takes (auth service + database) |
| `billing_notifications_total{type,result}` | Second password (`second_password`) and item lock (`item_lock`) requests: `success`, `failure`, `malformed` |
| `http_requests_received_total`, `http_request_duration_seconds` | HTTP requests per endpoint and status code |

Plus the standard .NET process metrics (CPU, memory, GC...).

Prometheus scrape config example:
```yaml
scrape_configs:
  - job_name: isror-billing
    static_configs:
      - targets: ["127.0.0.1:18080"]
```

Useful queries:
- Logins per minute by result: `sum by (result) (rate(billing_logins_total[5m])) * 60`
- Brute-force blocks in the last hour: `sum(increase(billing_logins_total{result=~"blocked_.*"}[1h]))`
- 95th percentile login time: `histogram_quantile(0.95, sum by (le) (rate(billing_login_duration_seconds_bucket[5m])))`

`invalid_signature` going up usually means a SaltKey mismatch with the GatewayServer, or someone calling the billing directly.

> ⚠️ **Security:** the `SaltKey` above is the public default that ships with the GatewayServer, so anyone can sign requests with it. If you can, patch your GatewayServer with your own value and set the same one here. Also never expose the billing port (`18080`) to the internet, only the GatewayServer needs to reach it.

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) to build. Run the tests with `dotnet test`.

## About services
We try to follow "micro-service" architechture approach, this is done to allow multiple different implementations of a given service, so that the community can easily customize the behavior of their application.

We currently have 3 types of services


### Authentication Service
This is the service used to authenticate a given user that's trying to login on the game. It's called by the gatewayserver via HTTP requests.

#### [Read more about Authentication Services here](/Services/Authentication/README.md)

### Notification Service
The notification service is used to send notifications such as secondary password reset or item lock to the user. This can be done via multiple implementations of the service.

#### [Read more about Notification Services here](/Services/Notification/README.md)

### Nation Ping Service
This is a service used to show the ping of the given server from the ingame login screen.
![App Screenshot](https://i.imgur.com/iOMPFBL.png)

#### [Read more about Ping Services here](/Services/Ping/README.md)