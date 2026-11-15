"""Back up and audit an existing SQLite database without changing its records."""
import argparse
import json
import sqlite3
from datetime import datetime, timezone
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("database", type=Path)
    parser.add_argument("--backup-directory", type=Path, required=True)
    args = parser.parse_args()
    source = args.database.resolve(strict=True)
    args.backup_directory.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    backup = args.backup_directory.resolve() / f"auracafe-phase0-{stamp}.db"
    # SQLite backup API includes committed WAL contents and produces a consistent snapshot.
    with sqlite3.connect(source.as_uri() + "?mode=ro", uri=True, timeout=10) as live:
        with sqlite3.connect(backup) as destination:
            live.backup(destination, pages=256, sleep=0.05)

    with sqlite3.connect(backup.as_uri() + "?mode=ro", uri=True) as db:
        tables = {row[0] for row in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        report = {
            "source": str(source), "backup": str(backup), "checked_at_utc": stamp,
            "integrity_check": db.execute("PRAGMA integrity_check").fetchall(),
            "foreign_key_check": db.execute("PRAGMA foreign_key_check").fetchall(),
            "migrations": db.execute('SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId').fetchall()
                if "__EFMigrationsHistory" in tables else [],
            "counts": {}, "checks": {},
        }
        for table in ("Categories", "Products", "Staff", "WorkShifts", "Orders", "OrderItems", "Payments"):
            report["counts"][table] = db.execute(f'SELECT COUNT(*) FROM "{table}"').fetchone()[0] if table in tables else None
        if {"Payments", "Orders"} <= tables:
            checks = {
                "duplicate_payments": "SELECT OrderId, COUNT(*) FROM Payments GROUP BY OrderId HAVING COUNT(*) > 1",
                "amount_or_order_state_mismatch": "SELECT p.Id FROM Payments p JOIN Orders o ON o.Id=p.OrderId WHERE p.AmountCents<>o.TotalCents OR o.Status<>'Paid'",
                "invalid_change": "SELECT Id FROM Payments WHERE ChangeCents<>CashReceivedCents-AmountCents OR ChangeCents<0",
                "paid_without_completed_payment": "SELECT o.Id FROM Orders o LEFT JOIN Payments p ON p.OrderId=o.Id AND p.Status='Completed' WHERE o.Status='Paid' AND p.Id IS NULL",
                "invalid_payment_state": "SELECT Id FROM Payments WHERE Method<>'Cash' OR Status<>'Completed' OR AmountCents<0",
            }
            report["checks"] = {key: db.execute(query).fetchall() for key, query in checks.items()}
            report["payment_indexes"] = db.execute("SELECT name, sql FROM sqlite_master WHERE type='index' AND tbl_name='Payments'").fetchall()
        report["database_checks_passed"] = (
            report["integrity_check"] == [("ok",)] and not report["foreign_key_check"]
            and {"Payments", "Orders"} <= tables
            and not any(report["checks"].values())
        )
        print(json.dumps(report, indent=2))
        return 0 if report["database_checks_passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
