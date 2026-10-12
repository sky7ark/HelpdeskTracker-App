# KB-001: "Error 500" when uploading a large file to the Licensing Portal

**Application:** Licensing Portal  **Category:** Application Error  **Related incident:** #1006
**Last reviewed:** October 2026

## Symptoms
- Users get an error page or HTTP 500/413 when they upload a PDF.
- It happens only with files larger than about 4 MB. Smaller files upload normally.

## Root cause
The IIS request filtering limit (`maxAllowedContentLength`) on the web server was set to 4 MB.
IIS rejects larger requests before they reach the application, so the application log shows nothing.

## How to confirm
1. Ask the user for the file size, or try uploading a test file over 4 MB.
2. On the web server, check the IIS log (`C:\inetpub\logs\LogFiles\W3SVC<n>`) for status `413`
   or `404.13` against the upload URL.

## Fix
In the site's `web.config`, raise the limit (the value is in bytes; 20 MB shown here):
```xml
<system.webServer>
  <security>
    <requestFiltering>
      <requestLimits maxAllowedContentLength="20971520" />
    </requestFiltering>
  </security>
</system.webServer>
```
Recycle the application pool, then test with a file between 4 and 20 MB.

## Workaround (until the fix is deployed)
Ask the user to compress the PDF or split it into smaller files.

## Message to the user
> The upload problem was caused by a file size limit on our web server. We've raised the limit
> to 20 MB, so you can now upload your document again. Please let us know if you have any other issues.
