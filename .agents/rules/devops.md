---
trigger: always_on
---

# ArborGraph — Permanent Development, GitHub, Website & Release Rules

## 1. Main Objective

Whenever you implement a feature, fix a bug, modify the application, update documentation, or complete a development task, follow the complete development and publishing workflow below.

Do not stop after editing local files. Your responsibility is to take completed work through testing, GitHub synchronization, website updates when needed, and release preparation.

First, inspect the existing project structure, Git configuration, repository, website deployment setup, build scripts, and release workflow. Reuse the existing configuration instead of creating a new repository, replacing the project architecture, or rebuilding the deployment pipeline unnecessarily.

## 2. Required Workflow After Every Completed Task

### Step 1 — Inspect and Implement
- Understand the user's request and inspect the relevant files before making changes.
- Implement only the requested functionality and necessary supporting changes.
- Preserve existing features, UI, settings, and application behavior unless changes are explicitly required.
- Avoid unrelated refactoring and unnecessary dependencies.
- Keep the existing project name, repository, and deployment configuration.

### Step 2 — Test and Validate
- Run the relevant tests, checks, and build commands.
- For the desktop application, verify that the application builds and that the changed functionality works.
- For the website, verify the website build and check affected pages, links, assets, and downloads.
- Check for regressions in related functionality.
- Never claim that a test passed unless it actually ran successfully.
- If a test or build fails, diagnose and fix the issue before publishing whenever possible.
- If an issue cannot be resolved, explain the blocker and do not publish a broken release.

### Step 3 — Update Documentation
Update relevant documentation whenever behavior, installation, configuration, features, or release instructions change.

Keep these synchronized where applicable:
- README.md
- CHANGELOG.md
- Version information
- Installation and usage instructions
- Website documentation and feature descriptions
- Release notes and download links

Do not modify files that are unrelated to the completed task.

### Step 4 — Synchronize GitHub
After the changes pass validation:

1. Inspect `git status`, the current branch, configured remotes, and recent commits.
2. Review the diff to ensure that only intended changes are included.
3. Never overwrite unrelated user changes or discard uncommitted work.
4. Never commit credentials, tokens, `.env` files, private user data, build secrets, or other sensitive files.
5. Commit the completed changes using a clear, descriptive commit message.
6. Push the commit to the correct existing GitHub repository and intended branch.
7. Confirm that the push succeeded and that the remote contains the intended commit.

If GitHub synchronization fails because of permissions, authentication, network errors, or conflicts, report the exact blocker. Never pretend that local commits have been pushed.

Do not force-push, rewrite shared history, or overwrite another person's changes without explicit authorization.

## 3. Website Synchronization

The project website must remain synchronized with the application and its latest public release.

After relevant changes:

- Identify the actual website source directory and deployment method.
- Update website content when features, screenshots, system requirements, installation instructions, or release information have changed.
- Update the download section to reference the latest compatible release asset when a new release is published.
- Keep the website's version information and changelog consistent with the application.
- Preserve the existing visual design and working website functionality.
- Verify that website links and download URLs point to valid destinations.
- Publish website source changes to the correct GitHub branch or deployment source.

If