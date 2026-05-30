# 更新 API 文档（Update API）

## 1. 接口作用
`GET /api/update/check` 用于让客户端检查是否有新版本可更新，并返回更新策略。

客户端可以基于返回结果决定：
- 是否提示用户更新
- 是否必须强制更新
- 从哪里下载安装包
- 是否展示更新说明

## 2. 接口定义
- 方法：`GET`
- 路径：`/api/update/check`

示例：
`http://localhost:5067/api/update/check?appId=wmsapp&platform=android&currentVersionCode=100&channel=prod`

## 3. 参数说明

| 参数名 | 必填 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `appId` | 是 | string | `wmsapp` | 应用标识，必须和服务端配置中的 `AppId` 匹配 |
| `platform` | 是 | string | `android` | 平台标识，必须和服务端配置中的 `Platform` 匹配 |
| `currentVersionCode` | 是 | int | `100` | 客户端当前版本号，必须 `>= 0` |
| `channel` | 否 | string | `prod` | 发布渠道，不传时使用服务端 `AppUpdate.DefaultChannel` |

## 4. 成功返回（HTTP 200）
返回结构为 `Result<UpdateCheckResponse>`：

```json
{
  "success": true,
  "message": "ok",
  "data": {
    "hasUpdate": true,
    "forceUpdate": true,
    "latestVersionName": "1.0.0",
    "latestVersionCode": 10000,
    "minSupportedVersionCode": 10000,
    "downloadUrl": "http://10.50.77.246/releases/wmsapp-1.0.0-10000.apk",
    "sha256": "AD8677A16BFE163FA7F00FAA2CE61541542E082563E938E0179B023F1831C61D",
    "releaseNotes": "修复全部亮灯可显示、增加默认亮灯颜色",
    "publishedAt": "2026-03-11T19:55:03+08:00"
  }
}
```

### 字段说明
- `hasUpdate`：是否有新版本
- `forceUpdate`：是否强制更新
- `latestVersionName`：最新展示版本号
- `latestVersionCode`：最新数值版本号
- `minSupportedVersionCode`：最低支持版本号
- `downloadUrl`：新版本安装包下载地址
- `sha256`：安装包哈希值
- `releaseNotes`：更新说明
- `publishedAt`：发布时间

## 5. 不同返回值应该做什么操作（客户端处理规范）

### 场景 A：HTTP 200，`success=true`，`hasUpdate=true`，`forceUpdate=true`
**客户端动作：**
1. 弹出不可关闭的强更提示。
2. 禁止进入业务主流程。
3. 仅保留“立即更新 / 重试”操作。
4. 下载完成后校验 `sha256`，通过后安装。

### 场景 B：HTTP 200，`success=true`，`hasUpdate=true`，`forceUpdate=false`
**客户端动作：**
1. 弹出可跳过的更新提示。
2. 用户选择“立即更新”则执行下载 + 安装。
3. 用户选择“稍后”可继续业务。

### 场景 C：HTTP 200，`success=true`，`hasUpdate=false`
**客户端动作：**
1. 显示“当前已是最新版本”。
2. 不触发下载、不影响业务流。

### 场景 D：HTTP 400（参数错误）
典型返回：
- 缺少 `appId` 或 `platform` 时，返回 ValidationProblemDetails。
- `currentVersionCode < 0` 时，返回 `Result.Fail("currentVersionCode must be >= 0.")`。

**客户端动作：**
1. 记录请求参数和错误信息到日志。
2. 提示“检查更新失败，请稍后重试”。
3. 不阻断业务（除你们有额外强制策略）。

### 场景 E：HTTP 404（无匹配发布记录）
典型返回：
`{"success":false,"message":"No release found for specified app/platform/channel.","data":null}`

**客户端动作：**
1. 视为“当前无可用发布策略”或“渠道配置错误”。
2. 记录 `appId/platform/channel` 到日志，便于运维排查。
3. 默认不阻断业务。

### 场景 F：HTTP 5xx（服务端异常）
**客户端动作：**
1. 记录错误并做指数退避重试（建议最多 3 次）。
2. 给出友好提示，不建议直接闪退。
3. 下次启动继续检查更新。

## 6. 版本比较与强更规则
服务端规则：
- `hasUpdate = latest.VersionCode > currentVersionCode`
- `forceUpdate = currentVersionCode < latest.MinSupportedVersionCode || (hasUpdate && latest.IsMandatory)`

## 7. 对接建议
1. 客户端每次启动 + 首页手动按钮都可触发检查更新。
2. 客户端下载后必须校验 `sha256`。
3. 服务端配置中的 `VersionCode` 要保持单调递增，避免判定错乱。
4. 建议后续统一参数缺失时的返回格式（目前缺参是 ValidationProblemDetails，其他错误是 Result）。
