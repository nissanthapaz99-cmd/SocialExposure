# Airtable setup for Social Exposure

The integration synchronizes approved website data in both directions while keeping authentication and authorization inside the website.

## 1. Create the Airtable base

Create these three tables. Field names must match exactly.

### Clients

| Field | Airtable type |
| --- | --- |
| Full Name | Single line text (primary field) |
| Website ID | Number (integer) |
| Email | Email |
| Company Name | Single line text |
| Phone Number | Phone number or single line text |
| Job Title | Single line text |
| Preferred Contact | Single line text |
| Account Status | Single line text |
| Created At | Date with time |

### Events

| Field | Airtable type |
| --- | --- |
| Event Name | Single line text (primary field) |
| Website ID | Number (integer) |
| Client Name | Single line text |
| Client Email | Email |
| Description | Long text |
| Start Date | Date |
| Deadline | Date |
| Status | Single line text |

### Designs

| Field | Airtable type |
| --- | --- |
| File Name | Single line text (primary field) |
| Website ID | Number (integer) |
| File URL | Single line text |
| Description | Long text |
| Version | Single line text |
| Uploaded At | Date with time |
| Event ID | Number (integer) |
| Client ID | Number (integer) |
| Status | Single line text |

`File URL` is text because local uploads use a relative path such as `/uploads/designs/file.png`.

## 2. Create a Personal Access Token

In Airtable's Builder Hub, create a Personal Access Token with access to this base and these scopes:

- `data.records:read`
- `data.records:write`

Do not put the token in `appsettings.json`, commit it, or paste it into chat.

## 3. Configure local development

Run these commands from the SocialExposure project folder, replacing the example values:

```powershell
dotnet user-secrets set "Airtable:PersonalAccessToken" "pat-your-token"
dotnet user-secrets set "Airtable:BaseId" "app-your-base-id"
dotnet user-secrets set "Airtable:Enabled" "true"
```

Restart the website. Sign in as Admin and open **Airtable Sync**, then select **Sync Now**.

## 4. Configure production hosting

Store these as private environment variables in the hosting provider:

```text
Airtable__PersonalAccessToken
Airtable__BaseId
Airtable__Enabled=true
```

The default table names are `Clients`, `Events`, and `Designs`. The default automatic sync interval is five minutes.

## Synchronization rules

- Website changes are exported to Airtable.
- Supported Airtable changes are imported into the website.
- New Airtable clients become unverified, unapproved Client accounts.
- Passwords, roles, verification, approvals and suspensions never come from Airtable.
- If both copies changed since the last sync, the website copy wins and the conflict is reported.
- Deleting an Airtable row never deletes the website record; it is recreated on the next sync.
