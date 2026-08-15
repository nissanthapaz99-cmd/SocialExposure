# Social Exposure

ASP.NET Core MVC portal for design approvals, projects, events, notifications, and messaging.

## Requirements

- .NET 10 SDK
- Git
- Visual Studio 2022 or VS Code with C# support

## Run the project

    git clone <repository-url>
    cd SocialExposure
    dotnet restore
    dotnet run

In VS Code, open the root SocialExposure folder and press F5. The included launch task rebuilds the Debug project before starting it.

## Shared test database

SocialExposure.db is intentionally committed as a SQLite test fixture. A fresh clone therefore starts with the same test users and sample records as the team.

Do not commit these temporary SQLite files:

- SocialExposure.db-wal
- SocialExposure.db-shm

Before committing database changes, stop every running copy of the application so SQLite can checkpoint changes into SocialExposure.db.

The relative connection string is:

    "DefaultConnection": "Data Source=SocialExposure.db"

This means the application uses the database file in the project root on every teammate's computer.

## Development accounts

Development mode ensures these accounts exist:

| Role | Email | Password |
| --- | --- | --- |
| Admin | admin@socialexposure.local | Admin123! |
| Staff | staff@socialexposure.local | Staff123! |

These accounts are for local testing only. Replace the passwords and remove development seeding before a production deployment.

## Client OTP login

When SMTP is not configured in Development, the generated OTP is displayed on the verification page. For real email delivery, configure the following outside Git using environment variables or .NET user secrets:

- EmailSettings__SenderName
- EmailSettings__SenderEmail
- EmailSettings__SmtpServer
- EmailSettings__Port
- EmailSettings__Username
- EmailSettings__Password

Never commit real SMTP credentials.

## Repository rules

- Do not commit bin, obj, .keys, WAL/SHM files, user secrets, or editor caches.
- Commit SocialExposure.db only when intentionally updating shared test data.
- Stop the app before committing the database.
- Run dotnet build before pushing.

## Current database strategy

The application uses SQLite and calls EnsureCreated during startup. This makes a fresh clone work with the shared test database or a newly created empty database. Existing databases are not automatically upgraded when the model changes, so schema changes must be coordinated by the team before replacing the shared fixture.
