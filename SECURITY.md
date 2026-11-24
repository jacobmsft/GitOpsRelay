# Security Policy

Please report suspected vulnerabilities privately to the repository maintainers
through GitHub's private vulnerability reporting feature. Do not open a public
issue for an unpatched security vulnerability.

Include the affected version or commit, impact, and the smallest reproduction
that demonstrates the issue. We will acknowledge reports and coordinate a fix
and disclosure timeline with the reporter.

GitOpsRelay must never put SCM credentials, webhook secrets, or authorization
tokens in a `GitSignal`. Treat repository URLs and all provider payloads as
untrusted input.
