# Azure Remote Rendering

Welcome to [Azure Remote Rendering](https://azure.microsoft.com/services/remote-rendering/).
This service enables you to render highly complex 3D models in real time on devices such as HoloLens 2.

Full documentation for Azure Remote Rendering can be found here:
<https://docs.microsoft.com/azure/remote-rendering>

This repository contains the following folders:

* Unity - This folder contains sample projects for use in the Unity game engine. 
   - Please note that you have to run a [script](https://docs.microsoft.com/azure/remote-rendering/quickstarts/render-model#clone-the-sample-app) before these projects can be opened in Unity.
* NativeCpp - This folder contains sample projects using Remote Rendering with native C++
* Scripts - This folder contains PowerShell scripts for interacting with the service (e.g. converting assets or launching rendering servers).
* Tools - This folder contains auxiliary utilities for working with Remote Rendering (e.g. tracing profiles to gather tracing information).

Another useful open-source tool which uses the C++ ARR SDK is Azure Remote Rendering Asset Tool (ARRT). This desktop application can be used to upload, convert and remotely render 3D models, using Azure Remote Rendering. Find the source code and binary releases on the [Azure Remote Rendering Asset Tool GitHub repository](https://github.com/Azure/azure-remote-rendering-asset-tool).

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit <https://cla.opensource.microsoft.com>.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the instructions
provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Support

Have an idea or suggestion? [Give us your feedback](https://feedback.azure.com/d365community/forum/46aa4cc0-fd24-ec11-b6e6-000d3a4f07b8)

Have an issue? [Refer to our Troubleshooting Guide](https://docs.microsoft.com/azure/remote-rendering/resources/troubleshoot) OR Ask the community on [Stack Overflow](https://stackoverflow.com/questions/tagged/azure-remote-rendering) and [Microsoft Q&A](https://docs.microsoft.com/answers/topics/azure-remote-rendering.html)