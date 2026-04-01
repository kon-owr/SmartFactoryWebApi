# SmartFactoryWebApi 更新接口测试报告

## 1. 测试概述
- 测试目标：验证 `GET /api/update/check` 的参数校验、默认渠道逻辑、更新判断与错误返回。
- 测试时间：2026-03-11
- 测试环境：
  - 服务地址：`http://localhost:5067`
  - 环境变量：`ASPNETCORE_ENVIRONMENT=Development`
  - 接口控制器：`UpdateController.Check`
- 测试结论：共 6 项，**通过 6 项，失败 0 项**。

## 2. 接口与参数说明
- 方法：`GET`
- 路由：`/api/update/check`

| 参数 | 必填 | 类型 | 示例 | 说明 |
|---|---|---|---|---|
| `appId` | 是 | string | `wmsapp` | 应用标识；用于匹配配置中的发布记录 |
| `platform` | 是 | string | `android` | 平台标识；用于匹配配置中的发布记录 |
| `currentVersionCode` | 是 | int (>=0) | `100` | 客户端当前版本号；用于比较是否有更新 |
| `channel` | 否 | string | `prod` | 发布渠道；不传时使用 `AppUpdate.DefaultChannel` |

## 3. 测试用例明细

| 用例ID | 测试项目 | 请求URL | 预期结果 | 实际结果 | 结论 |
|---|---|---|---|---|---|
| TC-01 | 正常更新检查（有更新） | `http://localhost:5067/api/update/check?appId=wmsapp&platform=android&currentVersionCode=9999&channel=prod` | HTTP 200；`success=true`；`data.hasUpdate=true`；返回版本与下载信息 | HTTP 200；`success=true`；`hasUpdate=true`；`latestVersionCode=10000` | 通过 |
| TC-02 | 缺少 `appId` | `http://localhost:5067/api/update/check?platform=android&currentVersionCode=100&channel=prod` | HTTP 400；参数缺失提示 | HTTP 400；返回 Model Validation 错误：`The appId field is required.` | 通过 |
| TC-03 | 缺少 `platform` | `http://localhost:5067/api/update/check?appId=wmsapp&currentVersionCode=100&channel=prod` | HTTP 400；参数缺失提示 | HTTP 400；返回 Model Validation 错误：`The platform field is required.` | 通过 |
| TC-04 | `currentVersionCode` 非法（-1） | `http://localhost:5067/api/update/check?appId=wmsapp&platform=android&currentVersionCode=-1&channel=prod` | HTTP 400；`currentVersionCode must be >= 0.` | HTTP 400；`{"success":false,"message":"currentVersionCode must be >= 0."}` | 通过 |
| TC-05 | 无匹配渠道（`channel=unknown`） | `http://localhost:5067/api/update/check?appId=wmsapp&platform=android&currentVersionCode=100&channel=unknown` | HTTP 404；`success=false`；无匹配发布记录 | HTTP 404；`{"success":false,"message":"No release found for specified app/platform/channel."}` | 通过 |
| TC-06 | 不传 `channel`（使用默认渠道） | `http://localhost:5067/api/update/check?appId=wmsapp&platform=android&currentVersionCode=100` | HTTP 200；按 `DefaultChannel=prod` 命中并返回更新信息 | HTTP 200；`success=true`；返回 `prod` 渠道最新版本（`latestVersionCode=10000`） | 通过 |

## 4. 关键实际响应摘录
- TC-01 / TC-06（成功）：
  - `latestVersionName=1.0.0`
  - `latestVersionCode=10000`
  - `downloadUrl=http://10.50.77.246/releases/wmsapp-1.0.0-10000.apk`
  - `forceUpdate=true`
- TC-02 / TC-03（缺参）：
  - 由 ASP.NET Core 自动模型校验拦截，返回标准 ValidationProblemDetails。
- TC-05（渠道不匹配）：
  - 返回 404，业务错误信息明确。

## 5. 发现的问题与建议
1. 参数缺失时返回格式不一致（ValidationProblemDetails），与业务返回 `Result<T>` 不统一。
   - 建议：若希望统一响应结构，可将 `appId/platform` 改为可空参数并在控制器中手动校验，或自定义全局 `InvalidModelStateResponseFactory`。
2. `AppUpdate.Releases` 当前存在 `VersionCode=102` 与 `VersionCode=10000`，系统会按最大 `VersionCode` 选最新。
   - 建议：统一版本编码规则，避免新旧版本比较出现“名称较低但编码更高”的情况。
3. 部分历史配置仍使用占位 `Sha256`（如 `PUT_REAL_SHA256_HERE`）。
   - 建议：所有可发布版本记录都写入真实 `Sha256`，避免客户端校验异常。

## 6. 复测建议
- 上线前最少复测：TC-01、TC-04、TC-05、TC-06。
- 发布后回归：用旧版 `currentVersionCode` 与当前版 `currentVersionCode` 各测一次，确认 `hasUpdate` 真假正确切换。
