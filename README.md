# .NET Core CAS（中央认证服务）客户端演示项目

## 1. 项目概述

本项目是一个基于 .NET Core 的最小化 Web 应用程序，旨在演示如何在不依赖任何特定于 CAS 的第三方库的情况下，手动与 CAS 服务器完成集成。项目通过标准的 `HttpClient` 和 `XmlDocument` 等内置工具，清晰地展示了 CAS 协议的核心认证流程。

对于任何希望将现有 .NET 系统与 CAS 服务器进行对接的开发者来说，本项目提供了一个轻量级、透明且易于理解的参考实现。

## 2. 核心认证流程

本项目的认证逻辑完全在 `Program.cs` 文件中实现，主要分为以下几个步骤：

1.  **发起登录 (访问 `/login`)**
    *   当用户尝试访问一个受保护的资源时（例如本项目的 `/protected`），应用程序的认证中间件会发现用户尚未登录。
    *   根据 Cookie 认证的配置，系统会将用户重定向到 `/login` 端点。
    *   此端点会构建一个指向 CAS 服务器登录页面的 URL，并在查询参数中附带一个 `service` 参数。这个 `service` 参数的值是 CAS 登录成功后需要将用户重定向回的地址（即本应用中的 `/signin-cas` 端点）。
    *   最终，用户浏览器将跳转到 CAS 服务器的登录页面。

2.  **票据验证 (访问 `/signin-cas`)**
    *   用户在 CAS 服务器上成功输入凭据后，CAS 会将用户重定向回第 1 步中提供的 `service` URL，并附带一个名为 `ticket` 的服务票据。
    *   `/signin-cas` 端点接收到这个 `ticket` 后，会立即向 CAS 服务器的 `/serviceValidate` 端点发起一个后台 HTTP GET 请求。此请求包含 `ticket` 和 `service` 两个参数，用于验证票据的有效性。
    *   CAS 服务器会返回一个 XML 格式的响应。

3.  **解析 XML 并创建会话**
    *   应用程序会解析上一步中收到的 XML 响应。
    *   如果 XML 中包含 `<cas:authenticationSuccess>` 节点，则证明票据有效。
    *   程序会从 XML 中提取用户名（位于 `<cas:user>` 节点）以及其他可能存在的用户属性（位于 `<cas:attributes>` 节点下）。
    *   利用提取到的用户信息，应用程序会创建一个 `ClaimsPrincipal` 对象，并使用 `HttpContext.SignInAsync` 方法来创建和颁发一个认证 Cookie。这标志着用户在本地应用中的会话已建立。
    *   随后，系统将用户重定向到最初请求的受保护页面（`/protected`）。

4.  **访问受保护资源 (访问 `/protected`)**
    *   由于此时用户已经拥有了有效的认证 Cookie，当他们访问任何被 `[RequireAuthorization]` 标记的端点时，都可以成功访问。
    *   本项目中的 `/protected` 页面会解析用户的 Cookie，并将其中包含的所有声明（Claims）信息（包括用户名和从 CAS 获取的其他属性）清晰地展示出来。

5.  **注销 (访问 `/logout`)**
    *   当用户点击“注销”按钮或访问 `/logout` 端点时，应用程序会首先调用 `HttpContext.SignOutAsync` 来删除本地的认证 Cookie。
    *   然后，它会将用户重定向到 CAS 服务器的 `/logout` 端点，以确保全局单点登录会话也被终止。

## 3. 如何集成到您的系统

将此逻辑集成到您自己的 .NET 项目中非常简单，只需关注以下几个关键点：

### 3.1. 调整配置参数

#### `appsettings.json`

这是您首先需要修改的配置文件。

```json
{
  "Cas": {
    "CasServerUrlBase": "https://cas.example.org/cas"
  }
}
```

*   **`Cas:CasServerUrlBase`**: **(必需)** 将此值替换为您的 CAS 服务器的基础 URL。这是所有 CAS 相关请求的前缀。

#### `Program.cs` (或您的启动代码)

在某些特殊部署环境下（例如在反向代理、负载均衡器或 Docker 容器后面），应用程序可能无法正确识别其面向公众的 URL。这会导致生成错误的 `service` 参数，从而使 CAS 验证失败。

为了解决这个问题，本项目已将 `service` 参数的 URL **硬编码**为一个固定的公开地址。您需要根据您的实际部署环境进行修改。

找到 `Program.cs` 中的以下代码行，并将其中的 URL 替换为您系统的实际公开回调地址：

```csharp
// 在 /login 端点中
var serviceUrl = Uri.EscapeDataString("https://your-public-facing-url/signin-cas");

// 在 /signin-cas 端点中
var serviceUrl = Uri.EscapeDataString("https://your-public-facing-url/signin-cas");
```

**重点**: 这两个 `serviceUrl` 的值必须完全一致，并且必须是 CAS 服务器可以访问到的您的应用程序的地址。

### 3.2. 依赖项

本项目没有引入任何外部的 NuGet 包来实现 CAS 功能，完全依赖 .NET 内置的库：

*   `Microsoft.AspNetCore.Authentication.Cookies`: 用于管理本地会话的 Cookie 认证。
*   `System.Net.Http.HttpClient`: 用于向 CAS 服务器发起票据验证请求。
*   `System.Xml.XmlDocument`: 用于解析 CAS 服务器返回的 XML 响应。

您只需确保您的项目包含了标准的 ASP.NET Core 依赖项即可。

### 3.3. 关键代码段

您可以将 `Program.cs` 中的 `/login`、`/signin-cas` 和 `/logout` 端点的实现逻辑，以及 `GetNamespaceManager` 辅助方法，直接复制到您的项目中。

*   **保护您的端点**: 要保护您自己的 API 或页面，只需添加 `.RequireAuthorization()` 即可，如下所示：

    ```csharp
    app.MapGet("/my-secure-data", (ClaimsPrincipal user) =>
    {
        // 只有通过 CAS 认证的用户才能访问这里
        return Results.Ok($"Hello, {user.Identity.Name}");
    }).RequireAuthorization();
    ```

### 4. 注意事项

*   **Service URL 的精确匹配**: CAS 协议要求验证票据时提供的 `service` 参数必须与发起登录时提供的 `service` 参数**完全一致**。任何微小的差异（例如 HTTP vs HTTPS，或不同的域名）都会导致验证失败。
*   **XML 命名空间**: CAS 返回的 XML 使用了 `cas:` 命名空间。本项目中的 `GetNamespaceManager` 方法处理了这个问题。如果您的 CAS 服务器使用了不同的命名空间或完全不使用，您需要相应地调整 XPath 查询。
*   **用户属性**: 不同的 CAS 服务器返回的用户属性可能不同。请检查您的 CAS 文档或实际的 XML 响应，以确定可以从 `<cas:attributes>` 节点中提取哪些有用的信息，并将其作为 Claims 添加到用户身份中。
*   **错误处理**: 本项目的 `/signin-cas` 端点包含了对票据验证失败（`<cas:authenticationFailure>`）和非 200 响应状态码的日志记录。在生产环境中，您可以根据这些错误信息执行更复杂的操作，例如向用户显示友好的错误页面。

