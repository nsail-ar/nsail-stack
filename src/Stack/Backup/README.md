# Backup

Attended backup and restore of a whole install, run by the host's own binary before it serves
anything. One archive is the install: the PostgreSQL database and the DataProtection key ring.
There is no HTTP endpoint and no unattended restore.

## Packages

| Package | What it is | Referenced by |
|---|---|---|
| `NSail.Backup` | `InstallConsole`, the archive format, the restore plan, the audit table | `NSail.BaseServices.WebApi` (key ring home); a host that offers the verbs |

## Use it

`NSail.BaseServices.WebApi` already references this package. A host adds the verbs right after
`Build()` and before `ApplyMigrations`, because a restore replaces the schema the migrations run
against:

```csharp
var app = builder.Build();

if (InstallConsole.Requested(args))
{
    Environment.ExitCode = await InstallConsole.Run<SampleDbContext>(app, args);

    return;
}

await app.ApplyMigrations<SampleDbContext>();
```

```bash
sample backup [<file or directory>] [--encrypt]
sample restore <archive> --confirm RESTORE
```

The binary's name is the product, taken from the context type (`SampleDbContext` is `Sample`).
`backup` into a directory (the default is the current one) names the file
`<product>-<yyyyMMdd-HHmmss>.nsail.zip`.

## Configuration

One key, optional: `Backup:ConnectionString`, the credential `pg_dump` connects with (a superuser or
`BYPASSRLS` role on the install's database; another host, port or database is refused); without it the dump uses the `DbContext`'s own connection
string. What the verbs use:

- **Passphrase:** `--encrypt` seals the dump and the key ring (AES-256-GCM, key from PBKDF2) under
  a passphrase read from `NSAIL_BACKUP_PASSPHRASE`, or asked twice at a terminal; never an
  argument. `restore` of an encrypted archive reads it the same way and refuses without it. The
  manifest stays readable and says `Encrypted`; an archive without `--encrypt` says so in the
  manifest, in the restore statement and when the backup finishes.
- **File mode:** the archive is created `0600` on Linux and macOS.
- **Application name:** `AddBaseWebApi` sets the DataProtection application name to the host's
  `ApplicationName`, so a restore to another content-root path unprotects the same secrets.
- **Database:** the `DbContext`'s own connection string names the database in the manifest and is
  what restore writes to; the dump credential must reach the same one.
- **Tools:** `pg_dump`, `pg_restore` and `psql` on the `PATH`, at the server's major version
  (`--format=custom`, `--no-owner`, `--no-privileges`). The password travels in `PGPASSWORD`,
  never as an argument.
- **Temp space:** a restore extracts the dump into the process's temp directory and renders it
  there as plain SQL for `psql`, so the temp volume needs room for the dump plus the whole
  database uncompressed; short of it the restore fails before the database is touched.
- **Key ring:** `<content root>/data/keyring` (`KeyRing.Home`). `AddBaseWebApi` persists the
  DataProtection keys there, so they travel in the archive; without them every sealed secret in a
  restored install is unreadable.
- **Role:** the dump needs a role that is a superuser or has `BYPASSRLS` when tenant tables force
  row-level security; `backup` checks first and refuses by name. A cell with `RequireRowSecurity` sets
  `Backup:ConnectionString` to one. Restore always uses the `DbContext`'s connection.
- **MultiDb:** a database per tenant is not covered: both verbs refuse with `REFUSED:` and exit code `4`.
- **Cancel:** while `pg_dump`/`pg_restore`/`psql` runs, Ctrl-C and SIGTERM (and the token `Run`
  is given) kill it and its process tree and the verb exits `130`; at a prompt the signal ends
  the process as usual.

| Exit code | Meaning |
|---|---|
| `0` | Done |
| `2` | Usage, or no archive at the path |
| `3` | Restore not confirmed; nothing written |
| `4` | Refused (`BackupFailure`) |
| `130` | Cancelled; the child tool was killed |

The archive holds `manifest.json` (`BackupManifest`: archive version, product, database, PostgreSQL
version, last applied migration, time, whether keys travel, whether and how it is encrypted), `database.dump` and `keyring/*.xml`.

- **Restore refuses before it writes**, via `RestorePlan.For`: an archive format newer than this
  build, a different product, a schema this build has no migration for, or a different PostgreSQL
  major version.
- **It then prints what it will overwrite** (database and key ring) and stops unless
  `--confirm RESTORE` is passed; at a terminal the word is typed a second time.
- **Restore is replace, not merge:** the `public` schema is dropped and rebuilt from the dump in one
  transaction (a dump that fails leaves the database as it was), and the key ring's keys are
  staged first and replaced by the archive's only once the database restored. The audit rows
  written before the restore are carried across it.
- **Both verbs are audited** in `nsail_backup_audit` (who, when, product, versions, archive). The
  table is created by raw SQL if absent, because it must be writable into whatever schema the
  archive carried.

## How it fits

- [BaseServices](../BaseServices/README.md): persists the key ring where the archive looks for it.
- [Data](../Data/README.md): the `DbContext`, its migrations and tenancy (the row-level policies
  behind the `BYPASSRLS` requirement).

## Going deeper

- Doctrine: [baseservices.md, Install verbs](../../../plugins/nsail-stack/doctrine/baseservices.md#install-verbs-backup-and-restore)
- Tests: [test/Stack/Backup](../../../test/Stack/Backup)
