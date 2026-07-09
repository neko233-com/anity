# Test Assets

此目录用于存放 AssetBundle 对照测试所需的资源文件。

## 使用方法

1. 从 Unity 编辑器导出 AssetBundle 到此目录
2. 或者使用 Unity 命令行工具导出：
   ```bash
   unity -batchmode -nographics -executeMethod BuildAssetBundles -quit
   ```

## 测试资源要求

- `test.bundle` - 基础测试资源包
- `textures.bundle` - 纹理资源包
- `prefabs.bundle` - 预制体资源包

## 注意事项

- 确保资源包使用 Unity 2022 LTS 导出
- 资源包应包含常用的资源类型（预制体、纹理、材质等）
- 保持资源包大小适中（建议 < 10MB）
