🎯 **What:**
Removed hardcoded database credentials (connection string) from `PedagangPulsa.Web/appsettings.json` and utility scripts (`DropEnums.cs`, `DropEnums/Program.cs`).

⚠️ **Risk:**
Storing production credentials in plain text within source code repositories is a severe security vulnerability. If the repository is exposed or if unauthorized users gain access to the codebase, they could compromise the remote Neon database (`neondb`). This could lead to data theft, modification, or deletion (Blast radius: the entire production database).

🛡️ **Solution:**
- Replaced the hardcoded connection string in `appsettings.json` with a safe local placeholder. In a production environment, this should be overridden using standard .NET environment variables (`ConnectionStrings__DefaultConnection`) or a secure vault.
- Updated the `DropEnums` scripts to read the connection string from a `DATABASE_URL` environment variable, falling back to a local placeholder if not set.
- Ensured no functionality is broken by compiling the solution and formatting.
