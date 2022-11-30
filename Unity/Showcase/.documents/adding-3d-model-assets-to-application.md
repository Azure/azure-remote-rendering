# Adding 3D Model Assets to ARR Showcase
A developer can customize the list of 3D model assets that appear within the application's menu. By default, the application only includes Azure Remote Render Rendering's [sample engine](https://docs.microsoft.com/en-us/azure/remote-rendering/samples/sample-model#built-in-sample-model) asset. To add more models, the developer has a simple and an advance option.

## Simple Method for Adding Models
The easiest way to include more models is by configuring an Azure blob container. This configuration is done during the application's [Remote Rendering setup](./implementation-notes.md#remote-rendering-service-extension). Once an Azure blob container is configured, the application will automatically ingest the container's [`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) blobs. 

The application's menu will be populate its model list with all remote rendering asset blobs ([`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models)) within the configured Azure blob container. The menu will label each model by using it's blob name, and display an associated image, if one is available. The associated image must have the same name without extension as the remote rendered asset, and be a PNG image. For example, the model at `https://{storageEndpoint}/{storageContainer}/remote-asset.arrAsset` will, by default, use the PNG image at `https://{storageEndpoint}/{storageContainer}/remote-asset.png`. If a different image or label is desired, then the more advance method is required.

## Advance Method for Adding Models
At startup, the application will first try to populate its menu with models listed in a `models.xml` blob. This blob is stored at the "root" of the configured Azure blob container; for example `https://{storageEndpoint}/{storageContainer}/models.xml`. After the `models.xml` blob is loaded, the application will search for any remaining [`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) blobs within the configured Azure blob container, as defined in the [simple method](#simple-method-for-adding-models). 

This advance method is useful for the following scenarios:
1. Defining custom labels or images.
2. Loading multiple models at once, represented as a single menu item.
3. Displaying a locally rendered model while the remote asset is loading.
4. Displaying a locally rendered model indefinitely.
5. Handling remote models that do not have remote colliders.
6. Adding remote rendered light sources.

### Creating a Model XML File
The `models.xml` file (or blob) is a list of model [`Containers`](#container-type). ARR Showcase will insert all enabled [`Containers`](#container-type) into it's menu. Each [`Container`](#container-type) is a set of one or more assets, along with metadata, such a label and image URL. A [`Container`](#container-type) asset can be a remote rendered model stored in an [`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) blob, a locally rendered model stored in an Unity Asset Package, or a remote rendered light source. 

See [models.xml](.samples/models.xml) for a sample, and [models.schema.xsd](.schemas/models.schema.xsd) the schema. 

#### Models Type
The `models.xml` file's root tag is `Models`. The `Models` type can only have a single child tag called [`Containers`](#container-type), which is defined has the following:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- | 
| Containers | `Container[]` | An array of model [`Container`](#container-type) entries. The array maybe empty, and has an unbounded maximum. |

#### Container Type
A [`Container`](#container-type) is a set of assets that can be loaded as a single operation. This set can include a verity types or assets that include:

1. Azure Remote Rendering assets stored in [`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) blob
2. Azure Remote Rendering light sources
3. Locally rendered Unity assets stored in an Unity Asset Package

The [`Container`](#container-type) tag can have any of the following child tags:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- |
| Enabled | `boolean` | A `boolean` defining if the model should be listed in the application's menu. If `true`, the container will be in the menu. If `false`, the container won't be in the menu. The default value is `true`. |
| HasColliders | `boolean` | A `boolean` that defines if all the [`.arrAssets`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) in this container have colliders. If `true`, the application assumes that there are remote colliders, such that remote raycasts can hit the loaded [`.arrAssets`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models). If `false`, the application assumes that there are no remote colliders and will create a local Unity collider to enable model interactions. The default value is `true`. It is possible to set this value to `false` even if there are be remote colliders for the [`.arrAssets`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models), in which case weird interactions may occur. So avoid setting this value to `false` if any [`.arrAssets`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) have remote colliders. For more information on model interactions and colliders, read the [Manipulating Models](https://docs.microsoft.com/en-us/azure/remote-rendering/tutorials/unity/manipulate-models/manipulate-models) tutorial for Azure Remote Rendering. |
| ImageUrl | `string` | An URL pointing to a PNG image. This image will be displayed in the application's menu. If left empty or undefined, the application will search for an image using the file or blob name of the first [`Model`](#model-type) in the `Items` array. For example, if the first [`Model`](#model-type) URL is `https://{storageEndpoint}/{storageContainer}/remote-asset.arrAsset`, the default image URL will be `https://{storageEndpoint}/{storageContainer}/remote-asset.png`. |
| Items | `Asset[]`| An array of assets that will be loaded as single operation. An entry in this list may be a [`Model`](#model-type), [`Placeholder`](#placeholder-type), [`PointLight`](#pointlight-type), [`Spotlight`](#spotlight-type), or [`DirectionalLight`](#directionallight-type). The [`Placeholder`](#placeholder-type) assets will be shown while all remote [`Models`](#model-type) are being loaded. Once all [`Models`](#model-type) are loaded, the [`Placeholder`](#placeholder-type) assets will be hidden. If there are no remote [`Models`](#model-type), the [`Placeholder`](#placeholder-type) assets will remain visible indefinitely. |
| Name | `string` | The name of the [`Container`](#container-type). The application's menu uses this string as a display label. If left empty or undefined, the application will label the [`Container`](#container-type) as *Model*. |
| Transform | [`Transform`](#transform-type) | A [`Transform`](#transform-type) describing the position, rotation, and scale of the entire container. This [`Transform`](#transform-type) is relative to the application's placement of this container. The default value is a position and rotation of `(0, 0, 0)`, and scale of `(1, 1, 1)`. |

#### Model Type
The [`Model`](#model-type) type represents a remote rendered asset backed by an [`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) blob. The [`Model`](#model-type) type can have the following child tags:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- |
| Enabled | `boolean` | Currently not used. |
| Name | `string` | The name of the asset. The application uses this string for naming a Unity game object, and for logging purposes. If left empty or undefined, the application will label the asset as *Model*. |
| Transform | [`Transform`](#transform-type) | A [`Transform`](#transform-type) type describing the position, rotation, and scale of the asset. This [`Transform`](#transform-type) is relative to the parent [`Container`](#container-type). The default value is a position and rotation of `(0, 0, 0)`, and scale of `(1, 1, 1)`. |
| Url | `string` | The URL to the [`.arrAsset`](https://docs.microsoft.com/en-us/azure/remote-rendering/concepts/models) blob. This blob must be in an Azure blob container that belongs to the configured Azure Storage account, and that is accessible by the Azure Remote Rendering service. See the [configuration](implementation-notes.md#remote-rendering-configuration-profiles) document for more information. |

This sample shows how to add the built-in Azure Remote Rendering [sample engine](https://docs.microsoft.com/en-us/azure/remote-rendering/samples/sample-model#built-in-sample-model) using `models.xml`.

```xml
<?xml version="1.0"?>
<Models
    xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Containers>
        <Container>
            <Name>Built In Engine</Name>
            <Transform>
                <Center>true</Center>
            </Transform>
            <ImageUrl>https://contosostorage.blob.core.windows.net/app-blob-container/built-in.png</ImageUrl>
            <Items>
                <Model>
                    <Name>Built In Engine</Name>
                    <Url>builtin://Engine</Url>
                </Model>
            </Items>
        </Container>
    </Containers>
</Models>
```

This sample shows how to add a custom Azure Remote Rendering model asset using `models.xml`.
```xml
<?xml version="1.0"?>
<Models
    xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Containers>
        <Container>
            <Name>Remote Asset</Name>
            <Transform>
                <Position>
                    <x>0</x>
                    <y>-1.0</y>
                    <z>0</z>
                </Position>
                <Rotation>
                    <x>0</x>
                    <y>0</y>
                    <z>0</z>
                </Rotation>
                <Scale>
                    <x>1.0</x>
                    <y>1.0</y>
                    <z>1.0</z>
                </Scale>
            </Transform>
            <ImageUrl>https://contosostorage.blob.core.windows.net/app-blob-container/remote-asset.png</ImageUrl>
            <Items>
                <Model>
                    <Name>Remote Asset</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/remote-asset.arrAsset</Url>
                </Model>
            </Items>
        </Container>
    </Containers>
</Models>
```

This sample shows how to load many remote rendered model assets as single menu operation.
```xml
<?xml version="1.0"?>
<Models
    xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Containers>
        <Container>
            <Name>Many Remote Assets</Name>
            <Transform>
                <Scale>
                    <x>0.01</x>
                    <y>0.01</y>
                    <z>0.01</z>
                </Scale>
                <Rotation>
                    <x>90</x>
                    <y>0</y>
                    <z>0</z>
                </Rotation>
                <Center>true</Center>
            </Transform>
            <ImageUrl>https://contosostorage.blob.core.windows.net/app-blob-container/many-remote-assets-image.png</ImageUrl>
            <Items>
                <Model>
                    <Name>Remote Asset 1</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/remote-asset-1.arrAsset</Url>
                </Model>
                <Model>
                    <Name>Remote Asset 2</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/remote-asset-2.arrAsset</Url>
                </Model>
                <Model>
                    <Name>Remote Asset 3</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/remote-asset-3.arrAsset</Url>
                </Model>
                <Model>
                    <Name>Remote Asset 4</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/remote-asset-4.arrAsset</Url>
                </Model>
                <Model>
                    <Name>Remote Asset 5</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/remote-asset-5.arrAsset</Url>
                </Model>
            </Items>
        </Container>
    </Containers>
</Models>
```

This sample shows how to handle remote rendered model assets without remote colliders.
```xml
<?xml version="1.0"?>
<Models
    xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Containers>
        <Container>
            <Name>Remote Asset without Remote Colliders</Name>
            <Transform>
                <Center>true</Center>
            </Transform>
            <HasColliders>false</HasColliders>
            <ImageUrl>https://contosostorage.blob.core.windows.net/app-blob-container/remote-asset-without-remote-collider.png</ImageUrl>
            <Items>
                <Model>
                    <Name>Remote Asset</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/remote-asset-without-remote-collider.arrAsset</Url>
                </Model>
            </Items>
        </Container>
    </Containers>
</Models>
```

#### Placeholder Type
The [`Placeholder`](#placeholder-type) type represents a locally rendered asset backed by a Unity Asset Package. Typically a [`Placeholder`](#placeholder-type) is shown only while remote [`Models`](#model-type) are being loaded. See the [Creating Unity Asset Bundle for Placeholder](#creating-unity-asset-bundle-for-placeholder) section for instructions on how to make Unity Asset Bundles for [`Placeholder`](#placeholder-type) assets.

The [`Placeholder`](#placeholder-type) type can contain the following child tags:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- |
| AssetName | `string` | The name, or path, of the asset that will be loaded. This asset must be within the Unity Asset Bundle defined by the `Url` tag. This asset name must be the path to the desired prefab within the asset bundle. For example, *Assets/Staging/AssetBundleModel.prefab* |
| Enabled | `boolean` | Currently not used. |
| Name | `string` | The name of the asset. The application uses this string for naming a Unity game object, and for logging purposes. If left empty or undefined, the application will label the asset as *Model*. |
| Transform | [`Transform`](#transform-type) | A [`Transform`](#transform-type) type describing the position, rotation, and scale of the asset. This [`Transform`](#transform-type) is relative to the parent [`Container`](#container-type). The default value is a position and rotation of `(0, 0, 0)`, and scale of `(1, 1, 1)`. |
| Url | `string` | The URL to a Unity Asset Bundle. This bundle must be in a publicly available location. The application currently doesn't use Azure Storage authentication to access these asset bundles. |

This sample shows how to add a [`Placeholder`](#placeholder-type) asset that is only shown while a remote asset is being loaded.

```xml
<?xml version="1.0"?>
<Models
    xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Containers>
        <Container>
            <Name>Remote Asset with Local Placeholder</Name>
            <Transform>
                <Center>true</Center>
            </Transform>
            <ImageUrl>https://contosostorage.blob.core.windows.net/app-blob-container/remote-local-asset-image.png</ImageUrl>
            <Items>
                <Placeholder>
                    <Name>Placeholder Asset</Name>
                    <Transform>
                        <Scale>
                            <x>1.1</x>
                            <y>1.1</y>
                            <z>1.1</z>
                        </Scale>
                    </Transform>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/placeholder-asset-bundle</Url>
                    <AssetName>Assets/placeholder-asset.prefab</AssetName>
                </Placeholder>
                <Model>
                    <Name>Remote Asset</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/another-remote-asset.arrAsset</Url>
                </Model>
            </Items>
        </Container>
    </Containers>
</Models>
```

This sample shows how to display an local rendered asset indefinitely, using a [`Placeholder`](#placeholder-type) model asset in a `models.xml` file.

```xml
<?xml version="1.0"?>
<Models
    xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Containers>
        <Container>
            <Name>Local Asset</Name>
            <Transform>
                <Center>true</Center>
                <MaxSize>
                    <x>2.0</x>
                    <y>2.0</y>
                    <z>2.0</z>
                </MaxSize>
            </Transform>
            <ImageUrl>https://contosostorage.blob.core.windows.net/app-blob-container/local-asset-image.png</ImageUrl>
            <Items>
                <Placeholder>
                    <Name>Local Asset</Name>
                    <Transform>
                        <Rotation>
                            <x>0</x>
                            <y>90</y>
                            <z>0</z>
                        </Rotation>
                    </Transform>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/local-asset-bundle</Url>
                    <AssetName>Assets/local-asset.prefab</AssetName>
                </Placeholder>
            </Items>
        </Container>
    </Containers>
</Models>
```

#### DirectionalLight Type
The [`DirectionalLight`](#directionallight-type) type represents a remote rendered component that simulates a light source that is very far away, such as the sun or the moon. Consequently, the light's defined position is ignored and only the orientation is used. The light shines into the direction of the negative z-axis of the owner game object. For more information on a point light within a remote rendered scene, see the Azure Remote Rendering [DirectionalLightComponent Class](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.remoterendering.directionallightcomponent?view=remoterendering) document.

The [`DirectionalLight`](#directionallight-type) type can contain the following child tags:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- |
| Color | [`Color`](#color-type) | The color of the light in Gamma color space. The default color is white. |
| Enabled | `boolean` | Currently not used. |
| Intensity | `float` | The overall brightness and range of the light. This value has no physical measure however it can be considered to be proportional to the physical power of the light source. If the light has a fall-off (point and spotlight) this value also defines the maximum range of light influence. An intensity of 1000 roughly has a range of 100 world units, but note this does not scale linearly. The default value if 10.0. |
| Name | `string` | The name of the asset. The application uses this string for naming a Unity game object, and for logging purposes. If left empty or undefined, the application will label the asset as *Model*. |
| Transform | [`Transform`](#transform-type) | A [`Transform`](#transform-type) type describing the rotation of the asset. The [`DirectionalLight`](#directionallight-type) type ignores the position and scale values. This [`Transform`](#transform-type) is relative to the parent [`Container`](#container-type). The default value is a position and rotation of zero, and scale of one. |

This sample shows how to add a directional light to the remote rendered scene, while also loading a remote rendered asset.

```xml
<?xml version="1.0"?>
<Models
    xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Containers>
        <Container>
            <Name>Directional Light</Name>
            <Transform>
                <Center>true</Center>
            </Transform>
            <ImageUrl>https://contosostorage.blob.core.windows.net/app-blob-container/directional-image.png</ImageUrl>
            <Items>
                <Model>
                    <Name>Sun</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/sun.arrAsset</Url>
                </Model>
                <DirectionalLight>
                    <Name>Directional Light Source</Name>
                    <Color>
                        <a>1.0</a>
                        <r>0.8</r>
                        <g>0.26</g>
                        <b>0.0</b>
                    </Color>
                    <Intensity>100</Intensity>
                </DirectionalLight>
            </Items>
        </Container>
    </Containers>
</Models>
```

#### PointLight Type
The [`PointLight`](#pointlight-type) type represents a remote rendered component that emits light uniformly into all directions. For more information on a point light within a remote rendered scene, see the Azure Remote Rendering [PointLightComponent Class](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.remoterendering.pointlightcomponent?view=remoterendering) document.

The [`PointLight`](#pointlight-type) type can contain the following child tags:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- |
| AttenuationCutoff | [`Vector2`](#vector2-type) | Custom interval of min/max distance over which the light's attenuated intensity is scaled down to zero. This feature can be used to enforce a smaller range of influence of a specific light. If not defined, these values are implicitly derived from the light's intensity. The default value is `(0, 0)`. |
| Color | [`Color`](#color-type) | The color of the light in Gamma color space. The default color is white. |
| Enabled | `boolean` | Currently not used. |
| Intensity | `float` | The overall brightness and range of the light. This value has no physical measure however it can be considered to be proportional to the physical power of the light source. If the light has a fall-off (point and spotlight) this value also defines the maximum range of light influence. An intensity of 1000 roughly has a range of 100 world units, but note this does not scale linearly. The default value if 10.0. |
| Length | `float` | The length of the light emitting shape. If `Length` and `Radius` are greater than zero, this value defines the length of a light emitting tube. Use case is a neon tube. The default value is zero. |
| Name | `string` | The name of the asset. The application uses this string for naming a Unity game object, and for logging purposes. If left empty or undefined, the application will label the asset as *Model*. |
| ProjectedCubeMap | `string` | An option URL that points to a cube-map texture to be projected onto surrounding geometry. This URL must point to an image asset in an Azure blob container that is accessible by the configured Azure Storage account, and also accessible by the remote rendering services. | 
| Radius | `float` | The radius of the light emitting shape. If greater than zero, the light emitting shape of the light source is a sphere of given radius as opposed to a point. This for instance affects the appearance of specular highlights. The default value is zero. |
| Transform | [`Transform`](#transform-type) | A [`Transform`](#transform-type) type describing the position and rotation. The [`PointLight`](#pointlight-type) type ignores the scale value. This [`Transform`](#transform-type) is relative to the parent [`Container`](#container-type). The default value is a position and rotation of zero, and scale of one. |

This sample shows how to add a point light to the remote rendered scene, while also loading a remote rendered asset.

```xml
<?xml version="1.0"?>
<Models
    xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Containers>
        <Container>
            <Name>Point Light</Name>
            <Transform>
                <Center>true</Center>
            </Transform>
            <ImageUrl>https://contosostorage.blob.core.windows.net/app-blob-container/point-light-image.png</ImageUrl>
            <Items>
                <Model>
                    <Name>Floor Lamp</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/floor.lamp.arrAsset</Url>
                </Model>
                <PointLight>
                    <Name>Point Light Source</Name>
                    <Transform>
                        <Position>
                            <x>0.0</x>
                            <y>0.75</y>
                            <z>0.0</z>
                        </Position>
                    </Transform>
                    <Color>
                        <a>1.0</a>
                        <r>0.98</r>
                        <g>1.0</g>
                        <b>0.43</b>
                    </Color>
                    <Intensity>4</Intensity>
                    <Radius>0.5</Radius>
                </PointLight>
            </Items>
        </Container>
    </Containers>
</Models>
```

#### Spotlight Type
The [`Spotlight`](#spotlight-type) type represents a remote rendered component that emits light within a directed cone. For more information on a point light within a remote rendered scene, see the Azure Remote Rendering [SpotLightComponent Class](https://docs.microsoft.com/en-us/dotnet/api/microsoft.azure.remoterendering.spotlightcomponent?view=remoterendering) document.

The [`Spotlight`](#spotlight-type) type can contain the following child tags:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- |
| Angle | [`Vector2`](#vector2-type) | The inner and outer angle, in degrees, of the spotlight cone. Everything within the inner angle is illuminated by the full brightness of the spot light source and a falloff is applied towards the outer angle which generates a penumbra-like effect. The default value is `(25.0, 35.0)`. |
| AttenuationCutoff | [`Vector2`](#vector2-type) | Custom interval of min/max distance over which the light's attenuated intensity is scaled down to zero. This feature can be used to enforce a smaller range of influence of a specific light. If not defined, these values are implicitly derived from the light's intensity. The default value is `(0, 0)`. |
| Color | [`Color`](#color-type) | The color of the light in Gamma color space. The default color is white. |
| Enabled | `boolean` | Currently not used. |
| Falloff | `float` | The strength of the light falloff between the inner and outer cone angle. Everything within the inner angle is illuminated by the full brightness of the spot light source and a falloff is applied towards the outer angle which generates a penumbra-like effect. The default value is `1.0`. |
| Intensity | `float` | The overall brightness and range of the light. This value has no physical measure however it can be considered to be proportional to the physical power of the light source. If the light has a fall-off (point and spotlight) this value also defines the maximum range of light influence. An intensity of 1000 roughly has a range of 100 world units, but note this does not scale linearly. The default value if 10.0. |
| Name | `string` | The name of the asset. The application uses this string for naming a Unity game object, and for logging purposes. If left empty or undefined, the application will label the asset as *Model*. |
| Projected2DTexture | `string` | An option URL that points to a 2D texture to be projected onto geometry. This URL must point to an image asset in an Azure blob container that is accessible by the configured Azure Storage account, and also accessible by the remote rendering services. | 
| Radius | `float` | The radius of the light emitting shape. If greater than zero, the light emitting shape of the light source is a sphere of given radius as opposed to a point. This for instance affects the appearance of specular highlights. The default value is zero. |
| Transform | [`Transform`](#transform-type) | A [`Transform`](#transform-type) type describing the position and rotation. The [`Spotlight`](#spotlight-type) type ignores the scale value. This [`Transform`](#transform-type) is relative to the parent [`Container`](#container-type). The default value is a position and rotation of zero, and scale of one. |

This sample shows how to add a spotlight to the remote rendered scene, while also loading a remote rendered asset.

```xml
<?xml version="1.0"?>
<Models
    xmlns:xsd="http://www.w3.org/2001/XMLSchema" 
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Containers>
        <Container>
            <Name>Spotlight</Name>
            <Transform>
                <Center>true</Center>
            </Transform>
            <ImageUrl>https://contosostorage.blob.core.windows.net/app-blob-container/spotlight-image.png</ImageUrl>
            <Items>
                <Model>
                    <Name>Flash Light</Name>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/flash.light.arrAsset</Url>
                </Model>
               <Spotlight>
                <Name>Spotlight Source</Name>
                    <Transform>
                        <Position>
                            <x>0.025</x>
                            <y>0.0</y>
                            <z>-0.2560001</z>
                        </Position>
                        <Rotation>
                            <x>0</x>
                            <y>90</y>
                            <z>0</z>
                        </Rotation>
                    </Transform>
                    <Color>
                        <a>1.0</a>
                        <r>0.66</r>
                        <g>0.64</g>
                        <b>0.38</b>
                    </Color>
                    <Intensity>50</Intensity>
                    <Radius>0.5</Radius>
                    <AttenuationCutoff>
                        <x>0.0</x>
                        <y>0.0</y>
                    </AttenuationCutoff>
                    <Angle>
                        <x>10.0</x>
                        <y>35.0</y>
                    </Angle>
                    <Falloff>0.8</Falloff>
                </Spotlight>
            </Items> 
        </Container>
    </Containers>
</Models>
```

#### Transform Type
The [`Transform`](#transform-type) type describes the `Position`, `Rotation`, and `Scale` of a component, relative to its parent. This also has some helpers to center a geometric object, as well as set a minimum and maximum size of a geometric object. Light sources will ignore the `Scale`, `Center`, `MinSize`, and `MaxSize` values. The [`DirectionalLight`](#directionallight-type) type will also ignore the `Position` value.

The [`Transform`](#transform-type) type can contain the following child tags:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- |
| Center | `boolean` | If `true`, the geometric center of a mesh (or model) asset will be placed at its parent's origin. Also, if `true`, any set `Position` is ignored. The default value is `false`. Light sources ignore this property. |
| MaxSize | [`Vector3`](#vector3-type) | A vector describing the maximum geometric size of a mesh (or model) asset, in world (or global) space. If the asset's geometric size is bigger than this value, the asset's size is reduced. This value is applied after applying the `Scale` value, but before applying the `Position` and `Center` values. This value must be larger or equal to `MinSize`. Zero or negative values in the `MaxSize` vector are ignored. The default value is `(0, 0, 0)`. Light sources ignore this property. |
| MinSize | [`Vector3`](#vector3-type) | A vector describing the minimum geometric size of a mesh (or model) asset, in world (or global) space. If the asset's geometric size is smaller than this value, the asset's size is increased. This value is applied after applying the `Scale` value, but before applying the `Position` and `Center` values. This value must be smaller or equal to `MaxSize`. Zero or negative values in the `MinSize` vector are ignored. The default value is `(0, 0, 0)`. Light sources ignore this property. |
| Position | [`Vector3`](#vector3-type) | A vector describing the position of the component, relative to its parent. The default value is `(0, 0, 0)`. |
| Rotation | [`Vector3`](#vector3-type) | A vector describing the rotation, in degrees, of the component, relative to its parent. The default value is `(0, 0, 0)`. [`DirectionalLight`](#directionallight-type) types ignore this property. |
| Scale | [`Vector3`](#vector3-type) | A vector describing the scale of the component, relative to its parent. The default value is `(1, 1, 1)`. Light sources ignore this property. |

#### Vector3 Type
The [`Vector3`](#vector3-type) type describes a vector containing three `float` components, `X`, `Y`, and `Z`. The [`Vector3`](#vector3-type) type must contain the following child tags:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- |
| X | `float` | The x component of the vector. This value must be defined. |
| Y | `float` | The y component of the vector. This value must be defined. |
| Z | `float` | The z component of the vector. This value must be defined. |

#### Vector2 Type
The [`Vector2`](#vector2-type) type describes a vector containing two `float` components, `X` and `Y`. The [`Vector2`](#vector2-type) type must contain the following child tags:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- |
| X | `float` | The x component of the vector. This is sometimes used as a minimum value. This value must be defined. |
| Y | `float` | The y component of the vector. This is sometimes used as a maximum value. This value must be defined. |

#### Color Type
The [`Color`](#color-type) type describes an ARGB color within the Gamma color space. The [`Color`](#color-type) type must contain the following child tags:

| <div style="width:90px">Child Tag</div> | <div style="width:60px">Type</div> | Description |
| :--- | :--- | :--- |
| A | `float` | The alpha component of the color. This value must be between zero and one, inclusively. This value must be defined. |
| B | `float` | The blue component of the color. This value must be between zero and one, inclusively. This value must be defined. |
| G | `float` | The green component of the color. This value must be between zero and one, inclusively. This value must be defined. |
| R | `float` | The red component of the color. This value must be between zero and one, inclusively. This value must be defined. |

### Creating Unity Asset Bundle for Placeholder
The application's [`Placeholder`](#placeholder-type) models are loaded from Unity Asset Bundles which can be created using the process documented at Unity's [Building AssetBundle](https://docs.unity3d.com/Manual/AssetBundles-Building.html) manual. To help with this process, the ARR Showcase project provides a helper utility.

#### Using ARR Showcase's Asset Bundle Utility
To create a [`Placeholder`](#placeholder-type) asset bundle, first create a new prefab within the project's assets. This prefab will be what's packaged into a bundle be to used as [`Placeholder`](#placeholder-type). Next add the desired components to the newly created prefab; meshes, text, ect. Note, scripts and materials can be safely referenced by this prefab, assuming that these assets are in the final compiled application.

![Adding Placeholder Prefab To Assets](.images/create-asset-bundle-add-prefab.png)

Next, from the prefab inspector window, set the asset bundle name, the prefab will be packaged into this asset bundle. If there are no asset bundles defined, select *New...* and define a new asset bundle name.

![Set Placeholder Prefab;s Asset Bundle Name](.images/create-asset-bundle-prefab-inspector.png)

Finally, from Unity's tool bar, select *Builder > Build Asset Bundles*. This will create all the asset bundles defined in the project, and place the results in the selected directory.

![Building Asset Bundles using Unity Toolbar Menu](.images/create-asset-bundle-menu-item.png)

After the bundle creation finishes, upload the desired asset bundles to your Azure blob container. For example, expanding on the images shown thus far, the *sampleassetbundle* file will contain the *Assets\Staging\AssetBundleModel.prefab* object, and this file should be uploaded to the blob container.

Once the asset bundle is in the Azure blob container, add the [`Placeholder`](#placeholder-type) entry into a `models.xml` file.  For example, once again expanding on the shown images, the *Assets\Staging\AssetBundleModel.prefab* is defined as a [`Placeholder`](#placeholder-type) asset in the following sample:

```xml
<?xml version="1.0"?>
<Models
    xmlns:xsd="http://www.w3.org/2001/XMLSchema"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
    <Containers>
       <Container>
            <Name>Sample</Name>
            <Items>
                <Placeholder>
                    <Url>https://contosostorage.blob.core.windows.net/app-blob-container/sampleassetbundle</Url>
                    <AssetName>Assets/Staging/AssetBundleModel.prefab</AssetName>
                </Placeholder>
            </Items>
        </Container>
    </Containers>
</Models>
```

For more information on creating a `models.xml` file, see the [Advance Method for Adding Models](#advance-method-for-adding-models) section. 

### Creating a Local Models.xml
In addition to loading a `models.xml` blob from the configured Azure blob container, the UWP application will try to load local `models.xml` file stored in the application's `LocalState` directory. This file has the exact same format as the blob container's `models.xml`. However, all the images defined in the local `models.xml` cannot be within private blob containers. 
