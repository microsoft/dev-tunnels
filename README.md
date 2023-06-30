# Dev tunnels

Dev tunnels allows developers to securely expose local web services to the Internet, control who has access, and easily & debug your web applications from anywhere. Learn more at [Dev tunnels documentation](https://aka.ms/devtunnels/docs).

## SDK Feature Matrix

| Feature | C# | TypeScript | Java | Go | Rust |
|---|---|---|---|---|---|
| Management API | ✅ | ✅ | ✅ | ✅ | ✅ |
| Tunnel Client Connections | ✅ | ✅ | ✅ | ✅ | 🚧 |
| Tunnel Host Connections | ✅ | ✅ | ❌ | ❌ | 🚧 |
| Reconnection | ✅ | ✅ | 🗓️ | 🗓️ | ❌ |
| SSH-level Reconnection | ✅ | ✅ | ❌ | ❌ | ❌ |
| Automatic tunnel access token refresh | ✅ | ✅ | 🗓️ | 🗓️ | ❌ |

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
- [On .NET Live - Develop webhooks locally with tunnels](https://www.youtube.com/watch?v=T4HiInCKGzA)
- [ASP.NET Community Standup - Dev tunnels in Visual Studio for ASP.NET Core projects](https://www.youtube.com/watch?v=B9K9eseNcKE)
- [New Visual Studio Feature is a Game Changer for API Developers - Put localhost Online](https://www.youtube.com/watch?v=NPJhrftkqeg)
- [Connect Any Client, Anywhere to localhost with Visual Studio Dev Tunnels!](https://www.youtube.com/watch?v=azuC8SFHWp8)
- [Dev Tunnels Visual Studio in 10 Minutes or Less](https://www.youtube.com/watch?v=kdaHwOkQf7c)
- [Share Local Web Services Across the Internet with Dev Tunnels CLI](https://www.youtube.com/watch?v=doUDcQNoy38)
- [ChatGPT Plugin development with Visual Studio](https://www.youtube.com/watch?v=iB9oxyJZhSA)

## Contributing & Feedback

Have a question or feedback? There are many ways to contribute.

- [Up-vote a feature or request a new one](https://github.com/microsoft/dev-tunnels/issues?q=is%3Aissue+is%3Aopen+label%3Afeature-request)
- Search [existing bugs](https://github.com/microsoft/dev-tunnels/issues?q=is%3Aissue+is%3Aopen+label%3Abug) or [file a new issue](https://github.com/microsoft/dev-tunnels/issues/new)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft 
trademarks or logos is subject to and must follow 
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship.
Any use of third-party trademarks or logos are subject to those third-party's policies.
