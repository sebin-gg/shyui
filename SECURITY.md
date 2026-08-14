# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

Please **do not** open a public issue for security vulnerabilities.

Instead, report privately via GitHub's Security Advisories:

1. Go to the repository's **Security** tab.
2. Click **Report a vulnerability** and follow the form.

You can expect an acknowledgment within 5 business days. If the report is
accepted, a fix will be released and a public advisory published once it is
safe to do so.

## Scope

Shy UI runs with the privileges of the user who launches it. Any window
manipulation is performed with standard Win32 APIs against the foreground
window. Reports involving privilege escalation, sensitive data leakage, or
registry misuse are in scope.