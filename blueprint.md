
# Project Blueprint

## 1. Overview

This project is a .NET web application that integrates with a CAS server for authentication.

## 2. Style, Design, and Features

### Initial Version

*   **.NET Web Application:** A simple ASP.NET Core application.
*   **CAS Integration:** The application redirects unauthenticated users to a CAS server for login. After successful authentication, the user is redirected back to the application.

## 3. Current Plan

### Integrate CAS Authentication

*   **Goal:** Add CAS authentication to the application.
*   **Steps:**
    1.  Add the `Genetec.Sdk.Authentication.Cas` NuGet package.
    2.  Configure the CAS client in `appsettings.json`.
    3.  Add the CAS authentication services and middleware in `Program.cs`.
    4.  Create a protected endpoint to test the authentication.
    5.  Add login and logout functionality.
