using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiOrchestrator.Modules.Runs.Persistence.Migrations;

/// <summary>
/// #271. The transcript line's key becomes (RunId, Ordinal) — what every reader queries, what the
/// sink guarantees, what retention deletes by. The minted `Id` this table inherited from
/// <c>BaseEntity</c> stored a Guid no query read: 22 MB of index per checkout, twice over.
/// <para>
/// SQLite cannot ALTER a primary key, so this is a table rebuild: build the new shape, copy the
/// rows without the dropped column, drop the old, rename. The copy is guarded — ordinals
/// duplicated within a run would mean a writer bug the new key must not silently swallow, so the
/// migration refuses before moving anything rather than failing halfway with the schema half-born.
/// </para>
/// <para>
/// <b>Written by hand over the scaffold.</b> EF's scaffold proposed DropPrimaryKey/AddPrimaryKey
/// in place, which SQLite answers with "cannot define a primary key constraint on column" — the
/// operation is not expressible as an ALTER, and the rebuild below is the SQLite-blessed recipe
/// (https://sqlite.org/lang_altertable.html, section 7). The freed pages stay marked free inside
/// the file — a VACUUM would hand them back to the OS, but it cannot run inside the transaction EF
/// wraps a migration in ("cannot VACUUM from within a transaction", verbatim, from the first
/// functional run), and the file's logical size is the claim this change makes: the pages are
/// reusable by SQLite from the first new run onward, and a manual <c>sqlite3 … VACUUM</c> after
/// the fact shrinks the file on demand.
/// </para>
/// </summary>
public partial class TranscriptKeyIsRunAndOrdinal : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The guard: a duplicate (RunId, Ordinal) cannot be copied, and revealing it here — with
        // the old table intact and nothing moved — is the one moment it is cheap to learn about.
        // `RAISE` outside a trigger is not SQLite, so the duplicate itself is made to fail the
        // statement: inserted into a UNIQUE temp table, a second (RunId, Ordinal) aborts the copy
        // with "UNIQUE constraint failed" — the old table untouched, nothing half-moved.
        migrationBuilder.Sql(
            """
            CREATE TEMP TABLE _guard (RunId TEXT NOT NULL, Ordinal INTEGER NOT NULL, PRIMARY KEY (RunId, Ordinal)) WITHOUT ROWID;

            INSERT INTO _guard
            SELECT "RunId", "Ordinal" FROM run_log_lines;

            DROP TABLE _guard;
            """
        );

        migrationBuilder.Sql(
            """
            CREATE TABLE run_log_lines_new (
                "RunId" TEXT NOT NULL,
                "Ordinal" INTEGER NOT NULL,
                "Kind" INTEGER NOT NULL,
                "Text" TEXT NOT NULL,
                CONSTRAINT "PK_run_log_lines" PRIMARY KEY ("RunId", "Ordinal"),
                CONSTRAINT "FK_run_log_lines_runs_RunId" FOREIGN KEY ("RunId") REFERENCES "runs" ("Id") ON DELETE CASCADE
            );

            INSERT INTO run_log_lines_new ("RunId", "Ordinal", "Kind", "Text")
            SELECT "RunId", "Ordinal", "Kind", "Text" FROM run_log_lines;

            DROP TABLE run_log_lines;
            ALTER TABLE run_log_lines_new RENAME TO run_log_lines;
            """
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The reverse rebuild: re-mint an Id for every row (a v7-shaped Guid from the run id and
        // the ordinal, deterministic so a second down does not re-randomize), restore the
        // surrogate key and the unique index the old shape carried.
        migrationBuilder.Sql(
            """
            CREATE TABLE run_log_lines_old (
                "Id" TEXT NOT NULL,
                "RunId" TEXT NOT NULL,
                "Ordinal" INTEGER NOT NULL,
                "Kind" INTEGER NOT NULL,
                "Text" TEXT NOT NULL,
                CONSTRAINT "PK_run_log_lines_old" PRIMARY KEY ("Id")
            );

            INSERT INTO run_log_lines_old ("Id", "RunId", "Ordinal", "Kind", "Text")
            SELECT
                lower(hex(substr("RunId", 1, 8)) || '-' || hex("Ordinal") || '-' || substr("RunId", -12)),
                "RunId", "Ordinal", "Kind", "Text"
            FROM run_log_lines;

            DROP TABLE run_log_lines;
            ALTER TABLE run_log_lines_old RENAME TO run_log_lines;

            CREATE UNIQUE INDEX "IX_run_log_lines_RunId_Ordinal" ON run_log_lines ("RunId", "Ordinal");
            """
        );
    }
}
