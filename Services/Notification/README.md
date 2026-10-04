# Notification Service
- [Notification Service](#notification-service)
    * [**Email (_Recommended - Default_)**](#--email---recommended---default----)
        + [***Requirements***:](#---requirements----)
        + [Steps](#steps)
        + [Customizing the emails](#customizing-the-emails)
    * [**Ferre's**](#--ferre-s--)
        + [***Requirements***:](#---requirements-----1)
        + [Steps](#steps-1)

<small><i><a href='http://ecotrust-canada.github.io/markdown-toc/'>Table of contents generated with markdown-toc</a></i></small>

The notification service is used to send notifications such as secondary password reset or item lock to the user. This can be done via multiple implementations of the service.


## **Email (_Recommended - Default_)**
This is the same implementation as the original used by official ISROR.

It's safer than other methods because it doesn't store the item lock on plaintext on the database, instead it works by receiving a request from the gatewayserver to send an item lock code to the user via email. 

We use **SMTP** for this, which you can configure on appsettings by adding your SMTP provider. 

We've used GMAIL for testing and you can learn how to get your account ready following this: https://code-maze.com/aspnetcore-send-email/ 

### ***Requirements***:
- Enable SMTP on your email provider (you can follow the steps from here https://code-maze.com/aspnetcore-send-email/)
- Place credentials on appsettings.json

```json
{
  ...
  "NotificationService": {
    "Type": "Email"
  },
  "EmailService": {
    "From": "yourEmail",
    "FromFriendlyName": "YourServerName??",
    "SmtpServer": "smtp.gmail.com",
    "UseSSL": true,
    "Port": 465,
    "Username": "FOLLOW https://code-maze.com/aspnetcore-send-email/",
    "Password": "FOLLOW https://code-maze.com/aspnetcore-send-email/",
    "SkipTokenValidation": false
  },
  ...
}
```

### Steps
1. Enable SMTP on your service provider (can be gmail or any of your choosing)
2. Configure appsettings.json

### Customizing the emails
The subject and body of both emails can be changed under `EmailService:Templates`, for example to translate them. You only need to set what you want to change; anything you leave out keeps the default text.

Available placeholders: `{ServerName}` (the `FromFriendlyName`), `{Code}`, `{Email}` and `{Jid}`.

```json
{
  "EmailService": {
    ...
    "Templates": {
      "SecondPassword": {
        "Subject": "[{ServerName}] Yeni ikinci şifreniz",
        "Body": "{ServerName} sunucusu için yeni ikinci şifreniz: {Code}"
      },
      "ItemLock": {
        "Subject": "[{ServerName}] Item kilit kodu",
        "BodyFile": "EmailTemplates/item-lock.example.html", // 👈 Optional: read the body from a file
        "IsHtml": true                                      // 👈 Send it as HTML
      }
    }
  }
}
```

- `BodyFile` can be absolute or relative to the billing folder. It's read on every email, so you can edit it without restarting. If it can't be read, `Body` is used instead (and an error is logged).
- With `IsHtml: true`, placeholder values are HTML-encoded.
- Two ready-to-edit HTML examples are included in [`EmailTemplates`](/EmailTemplates).

## **Ferre's**

This method stores the item lock password in plaintext on the database so it can be fetched by other systems. 

Works by receiving the same request than email method from the gatewayserver, but instead of sending it to the user via email, it stores it somewhere on the database. 

**Please note:** **_Using this method is insecure by extension; if anyone has gotten access to the user's credentials, and the item lock code is displayed on the website because they are read from the database, there's no point in locking the item at all, as the same attacker can login to the website to see the latest item code lock._**

### ***Requirements***: 
- Create or alter an existing table to store the item lock code
- Create or alter an stored procedure to save the lock code 
- Create your own way to fetch and display **securely** the item lock code to the user


```json
{
...
  "NotificationService": {
    "Type": "Ferre"
  }
...
}
```

### Steps
1-Execute this [Update_TbUser.sql Script](/Database/CommunityProvided/F3rreNotificationService/Scripts/UpdateTbUser.sql) (the procedures below use the columns it adds)

2-Execute this [Update_ItemLock.sql Script](/Database/CommunityProvided/F3rreNotificationService/Scripts/Update_ItemLock.sql)

3-Execute this [Update_SecPassWord.sql Script](/Database/CommunityProvided/F3rreNotificationService/Scripts/Update_SecPassWord.sql)

> **Already installed these procedures before?** Older versions of both scripts had a bug (`WHERE JID = JID`) that only checked if the email existed on *any* account. Re-run both scripts using `ALTER PROCEDURE` to get the fix.

4-Choose **Type : Ferre** from "*appsettings.json*" 👈👀

![App Screenshot](https://i.imgur.com/Ph3nPcb.png)

![App Screenshot](https://i.imgur.com/TZZ3WKM.png)

![App Screenshot](https://i.imgur.com/aQrD86u.png)

