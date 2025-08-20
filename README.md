[![NuGet version](https://badge.fury.io/nu/Microsoft.DevTunnels.Connections.svg)](https://badge.fury.io/nu/Microsoft.DevTunnels.Connections)
[![npm version](https://badge.fury.io/js/%40microsoft%2Fdev-tunnels-connections.svg)](https://badge.fury.io/js/%40microsoft%2Fdev-tunnels-connections)

# Dev tunnels

Dev tunnels allows developers to securely expose local web services to the Internet, control who has access, and easily & debug your web applications from anywhere. Learn more at [Dev tunnels documentation](https://aka.ms/devtunnels/docs).

## SDK Feature Matrix

| Feature | C# | TypeScript | Java | Go | Rust |
|---|---|---|---|---|---|
| Management API | ✅ | ✅ | ✅ | ✅ | ✅ |
| Tunnel Client Connections | ✅ | ✅ | ✅ | ✅ | ✅ |
| Tunnel Host Connections | ✅ | ✅ | ❌ | ❌ | ✅ |
| Reconnection | ✅ | ✅ | ❌ | ❌ | ❌ |
| SSH-level Reconnection | ✅ | ✅ | ❌ | ❌ | ❌ |
| Automatic tunnel access token refresh | ✅ | ✅ | ❌ | ❌ | ❌ |
| Ssh Keep-alive | ✅ | ✅ | ❌ | ❌ | ❌ |

✅ - Supported  
🚧 - In Progress  
❌ - Not Supported  
🗓️ - Planned  

## Resources

### Documentation
- [Dev tunnels documentation](https://aka.ms/devtunnels/docs)
- [Announcing the public preview of the devtunnel CLI](http://aka.ms/devtunnels/blog/cli)
- [Dev tunnels in Visual Studio 2022](http://aka.ms/devtunnels/vs)
- [Where else is dev tunnels used?](https://learn.microsoft.com/en-us/azure/developer/dev-tunnels/faq#where-else-is-dev-tunnels-used)

### Videos

#### Official
- [Securely test and debug your web apps and webhooks with dev tunnels](https://www.youtube.com/watch?v=yBiOGgUFD68)
- [Advanced developer tips and tricks in Visual Studio](https://youtu.be/Czr2M9qcdW4?t=491)

#### Community-created
- [ASP.NET Community Standup - Dev tunnels in Visual Studio for ASP.NET Core projects](https://youtu.be/B9K9eseNcKE?t=185)
- [New Visual Studio Feature is a Game Changer for API Developers - Put localhost Online](https://www.youtube.com/watch?v=NPJhrftkqeg)
- [Connect Any Client, Anywhere to localhost with Visual Studio Dev Tunnels!](https://www.youtube.com/watch?v=azuC8SFHWp8)
- [Dev Tunnels Visual Studio in 10 Minutes or Less](https://www.youtube.com/watch?v=kdaHwOkQf7c)
- [Share Local Web Services Across the Internet with Dev Tunnels CLI](https://www.youtube.com/watch?v=doUDcQNoy38)
- [ChatGPT Plugin development with Visual Studio](https://www.youtube.com/watch?v=iB9oxyJZhSA)

## Feedback

Have a question or feedback? There are many ways to submit feedback.

- [Up-vote a feature or request a new one](https://github.com/microsoft/dev-tunnels/issues?q=is%3Aissue+is%3Aopen+label%3Afeature-request)
- Search [existing bugs](https://github.com/microsoft/dev-tunnels/issues?q=is%3Aissue+is%3Aopen+label%3Abug) or [file a new issue](https://github.com/microsoft/dev-tunnels/issues/new)

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Security

Microsoft takes the security of our software products and services seriously, which includes all source code repositories managed through our GitHub organizations, which include [Microsoft](https://github.com/Microsoft), [Azure](https://github.com/Azure), [DotNet](https://github.com/dotnet), [AspNet](https://github.com/aspnet), [Xamarin](https://github.com/xamarin), and [our GitHub organizations](https://opensource.microsoft.com/).

If you believe you have found a security vulnerability in any Microsoft-owned repository that meets [Microsoft's definition of a security vulnerability](https://docs.microsoft.com/en-us/previous-versions/tn-archive/cc751383(v=technet.10)), please report it to us as described in [Security](SECURITY.md).

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
