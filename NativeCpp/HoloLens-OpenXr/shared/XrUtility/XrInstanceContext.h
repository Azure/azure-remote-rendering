// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma once

#include "XrHandle.h"
#include "XrStruct.h"
#include "XrString.h"

namespace xr {
    struct InstanceContext {
        const XrInstance Handle;
        const NameVersion AppInfo;
        const NameVersion EngineInfo;
        const XrInstanceProperties Properties{XR_TYPE_INSTANCE_PROPERTIES};
        const XrPath LeftHandPath;
        const XrPath RightHandPath;

    public:
        InstanceContext(xr::InstanceHandle instance, NameVersion appInfo, NameVersion engineInfo, XrInstanceProperties instanceProperties)
            : Handle(instance.Get())
            , AppInfo(std::move(appInfo))
            , EngineInfo(std::move(engineInfo))
            , Properties(std::move(instanceProperties))
            , m_instance(std::move(instance))
            , LeftHandPath(xr::StringToPath(Handle, "/user/hand/left"))
            , RightHandPath(xr::StringToPath(Handle, "/user/hand/right")) {
        }

    private:
        xr::InstanceHandle m_instance;
    };

    inline InstanceContext CreateInstanceContext(NameVersion appInfo, NameVersion engineInfo, const std::vector<const char*>& extensions) {
        XrInstanceCreateInfo instanceCreateInfo{XR_TYPE_INSTANCE_CREATE_INFO};
        xr::SetEnabledExtensions(instanceCreateInfo, extensions);
        xr::SetApplicationInfo(instanceCreateInfo.applicationInfo, appInfo, engineInfo);

        xr::InstanceHandle instance;
        CHECK_XRCMD(xrCreateInstance(&instanceCreateInfo, instance.Put()));

        XrInstanceProperties instanceProperties{XR_TYPE_INSTANCE_PROPERTIES};
        CHECK_XRCMD(xrGetInstanceProperties(instance.Get(), &instanceProperties));

        return xr::InstanceContext(std::move(instance), std::move(appInfo), std::move(engineInfo), std::move(instanceProperties));
    }

} // namespace xr
