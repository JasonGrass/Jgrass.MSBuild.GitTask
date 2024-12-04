# Jgrass.MSBuild.GitTask

git task for msbuild, base `netstandard2.0`

## LargeFileInterceptTask

Implement interception of committed file size through git pre-commit hook. If the size exceeds the set value, then either prompt or intercept.

GitLargeFileInterceptHardLimit: Set the hard limit size (in bytes) where submission fails if exceeded. default value: 10000000;  
GitLargeFileInterceptSoftLimit: Set the soft limit size (in bytes) where a prompt is given if exceeded. default value: 1000000;  
LargeFileInterceptTaskEnable: Enable or disable this task; enabled by default. If set to any non-`true` value, the task functionality is disabled.  

## How to use

in csproj file:

```xml
<PropertyGroup>
  <GitLargeFileInterceptHardLimit>10000000</GitLargeFileInterceptHardLimit>
  <GitLargeFileInterceptSoftLimit>1000000</GitLargeFileInterceptSoftLimit>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Jgrass.MSBuild.GitTask" Version="1.0.0-alpha" />
</ItemGroup>
```
