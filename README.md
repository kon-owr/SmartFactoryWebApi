# SmartFactoryWebApi

智能仓储管理系统（WMS）后端 API，基于 ASP.NET Core + Entity Framework Core + SQL Server。

## 技术栈

| 技术 | 版本 |
|------|------|
| .NET | 9.0 |
| ASP.NET Core | 9.0 |
| Entity Framework Core | 9.0 |
| SQL Server | 2008+（兼容级别 100） |
| OpenAPI | 内置 Swagger 支持 |

## 项目结构

```
SmartFactoryWebApi/
├── SmartFactoryWebApi/
│   ├── Controllers/           # API 控制器
│   ├── Services/              # 业务逻辑层
│   │   └── Interfaces/        # 服务接口
│   ├── DTO/                   # 数据传输对象
│   ├── Models/                # EF Core 实体（数据库表映射）
│   ├── Data/                  # DbContext + 数据库配置
│   ├── Options/               # 配置选项类
│   ├── sql/                   # 数据库迁移脚本
│   └── docs/                  # API 文档
```

## API 接口

### 入库模块 `/api/entry`

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `/api/entry/allocate` | 扫码分配库位（支持升序/降序、大盘步长） |
| POST | `/api/entry/commit` | 确认入库（事务性库存转移） |

### 拣货模块 `/api/pick`

| 方法 | 路由 | 说明 |
|------|------|------|
| POST | `/api/pick/exists` | 检查领料单是否存在 |
| POST | `/api/pick/reserve` | 自动分配并锁定条码（FIFO + Serializable 隔离） |
| POST | `/api/pick/lock` | 手动锁定条码 |
| POST | `/api/pick/unlock` | 解锁条码 |
| POST | `/api/pick/complete` | 完成拣货（释放库位 + 清理锁定 + 标记出库） |

### 应用更新 `/api/update`

| 方法 | 路由 | 说明 |
|------|------|------|
| GET | `/api/update/check` | 检查应用更新（版本比较、强制更新判断） |

## 数据库

### 连接配置

连接字符串在 `appsettings.json` 中配置：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=sWMS_Production;..."
  }
}
```

### 数据表

| 表名 | 实体 | 说明 |
|------|------|------|
| WMS_BAR_DETAIL | BarDetail | 条码明细（条码号、料号、库位、入库状态） |
| WMS_SHELF_DETAIL | ShelfDetail | 货架库位（行列坐标、步长、排序方向） |
| WMS_BIN_DETAIL | BinDetail | 库位主数据 |
| WMS_ITEM_STOCK | StockDetail | 库存明细 |
| WMS_ITEM_DETAIL | ItemDetail | 物料主数据 |
| WMS_PALLET_STATE | PalletDetail | 托盘-条码关联 |
| WMS_PICKING_APPLY | PickApply | 领料单 |
| WMS_PICKING_APPLY_DETAIL | PickDetail | 领料单明细 |
| WMS_WAREHOUSE_DETAIL | WarehouseDetail | 仓库信息 |
| CUS_PICKING_LOCK_BARNO | LockedBarNo | 拣货锁定条码（并发控制） |

### 迁移脚本

| 文件 | 说明 |
|------|------|
| `sql/20260313_pick_lock_indexes.sql` | 拣货锁定表唯一索引 + 复合索引 |
| `sql/20260319_add_bin_size.sql` | 货架库位新增 BIN_SIZE 字段（大盘货架步长=3） |
| `sql/20260320_add_warehouse_to_locked_barno.sql` | 锁定表新增仓库字段（仓库隔离） |

> 部署前请按日期顺序执行 `sql/` 目录下的迁移脚本。

## 核心业务逻辑

### 入库分配（EntryDetailService）

1. 解析条码（托盘码/散件码）
2. 防重复入库校验（IsRack 检查）
3. 按货架 SortDirection（ASC/DESC）+ BinSize 步长分配库位
4. 提交入库（事务：扣减源库位库存、新增目标库位库存、标记条码已上架）

### 拣货流程（PickDetailService）

1. 查询领料单 → FIFO 分配条码（排除已锁定 + 已出库条码）
2. Serializable 事务锁定条码（幂等检查防重复插入）
3. 完成拣货：释放库位占用 → 清理锁定记录 → 标记条码 IsRack='N'

### 仓库隔离

所有操作以 `DocNo + WarehouseLocation` 为复合键隔离，防止跨仓库数据污染。

## 快速开始

```bash
# 还原依赖
dotnet restore

# 开发环境运行
dotnet run --launch-profile http

# 发布
dotnet publish -c Release -r win-x64 -o ./publish
```

## 配置

| 文件 | 环境 | 说明 |
|------|------|------|
| `appsettings.json` | 基础 | 连接字符串、日志、版本配置 |
| `appsettings.Production.json` | Production | 生产环境覆盖 |

## 文档

| 文档 | 说明 |
|------|------|
| [docs/update-api-doc.zh-CN.md](docs/update-api-doc.zh-CN.md) | 更新检查 API 规格文档 |
| [docs/update-api-test-report.zh-CN.md](docs/update-api-test-report.zh-CN.md) | 更新 API 测试报告 |

## 版本

当前版本：**1.0.9**
