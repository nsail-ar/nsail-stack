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
sample backup [<file or directory>]
sample restore <archive> --confirm RESTORE
```

The binary's name is the product, taken from the context type (`SampleDbContext` is `Sample`).
`backup` into a directory (the default is the current one) names the file
`<product>-<yyyyMMdd-HHmmss>.nsail.zip`.

## Configuration

There are no configuration keys. What the verbs use:

- **Database:** the `DbContext`'s own connection string, so the database is the one the host runs on.
- **Tools:** `pg_dump` and `pg_restore` on the `PATH`, at the server's major version
  (`--format=custom`, `--no-owner`, `--no-privileges`). The password travels in `PGPASSWORD`,
  never as an argument.
- **Key ring:** `<content root>/data/keyring` (`KeyRing.Home`). `AddBaseWebApi` persists the
  DataProtection keys there, so they travel in the archive; without them every sealed secret in a
  restored install is unreadable.
- **Role:** the dump needs a role that is a superuser or has `BYPASSRLS` when tenant tables force
  row-level security; `backup` checks first and refuses by name.

| Exit code | Meaning |
|---|---|
| `0` | Done |
| `2` | Usage, or no archive at the path |
| `3` | Restore not confirmed; nothing written |
| `4` | Refused (`BackupFailure`) |

The archive holds `manifest.json` (`BackupManifest`: archive version, product, database, PostgreSQL
version, last applied migration, time, whether keys travel), `database.dump` and `keyring/*.xml`.

- **Restore refuses before it writes**, via `RestorePlan.For`: an archive format newer than this
  build, a different product, a schema this build has no migration for, or a different PostgreSQL
  major version.
- **It then prints what it will overwrite** (database and key ring) and stops unless
  `--confirm RESTORE` is passed; at a terminal the word is typed a second time.
- **Restore is replace, not merge:** the `public` schema is dropped and rebuilt from the dump, and
  the key ring's keys are replaced by the archive's.
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
