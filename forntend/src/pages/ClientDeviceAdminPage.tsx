import { useEffect, useMemo, useState } from "react";
import {
  Boxes,
  Building2,
  Monitor,
  RefreshCw,
  Search,
  ShieldCheck,
  ShieldOff,
} from "lucide-react";
import { apiFetch } from "../services/api";

interface DeviceBindingSummary {
  customerSoftwareId: number;

  customerId: number;
  customerName: string;
  customerCode: string;
  customerEnabled: boolean;

  softwareId: number;
  softwareName: string;
  softwareCode: string;
  softwareEnabled: boolean;

  bindingEnabled: boolean;

  deviceCount: number;
  enabledDeviceCount: number;
  disabledDeviceCount: number;
  lastUsedAt: string | null;
}

interface DeviceItem {
  id: number;
  installationId: string;
  deviceName: string;
  tokenPrefix: string;
  isEnabled: boolean;
  activatedAt: string;
  lastUsedAt: string | null;
  updatedAt: string;
}

interface DeviceDetailResult {
  customerSoftwareId: number;
  customerId: number;
  customerName: string;
  customerCode: string;
  softwareId: number;
  softwareName: string;
  softwareCode: string;
  bindingEnabled: boolean;
  devices: DeviceItem[];
}

/**
 * 管理员设备级自动更新授权管理。
 *
 * 这里展示的是“真实安装设备”，
 * 不再让管理员把共享 UpdateToken 当成主要管理对象。
 *
 * 旧版共享 Token 页面暂时继续保留，
 * 用于兼容已经部署出去的老客户端。
 */
function ClientDeviceAdminPage() {
  const [items, setItems] = useState<DeviceBindingSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState("");
  const [keyword, setKeyword] = useState("");

  const [detail, setDetail] = useState<DeviceDetailResult | null>(null);
  const [deviceLoading, setDeviceLoading] = useState(false);
  const [savingDeviceId, setSavingDeviceId] = useState<number | null>(null);

  async function loadBindings() {
    try {
      setLoading(true);
      setErrorMessage("");

      const response = await apiFetch("/api/client-device-admin/bindings");

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `加载更新设备失败：${response.status}`);
      }

      setItems((await response.json()) as DeviceBindingSummary[]);
    } catch (error) {
      console.error("加载更新设备授权失败：", error);
      setErrorMessage(
        error instanceof Error
          ? error.message
          : "加载更新设备授权失败",
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadBindings();
  }, []);

  const filteredItems = useMemo(() => {
    const value = keyword.trim().toLowerCase();

    if (!value) {
      return items;
    }

    return items.filter(
      (item) =>
        item.customerName.toLowerCase().includes(value) ||
        item.customerCode.toLowerCase().includes(value) ||
        item.softwareName.toLowerCase().includes(value) ||
        item.softwareCode.toLowerCase().includes(value),
    );
  }, [items, keyword]);

  async function openDevices(item: DeviceBindingSummary) {
    try {
      setDeviceLoading(true);
      setDetail(null);

      const response = await apiFetch(
        `/api/client-device-admin/bindings/${item.customerSoftwareId}/devices`,
      );

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `加载设备列表失败：${response.status}`);
      }

      setDetail((await response.json()) as DeviceDetailResult);
    } catch (error) {
      console.error("加载设备列表失败：", error);
      alert(error instanceof Error ? error.message : "加载设备列表失败");
    } finally {
      setDeviceLoading(false);
    }
  }

  async function setDeviceEnabled(
    device: DeviceItem,
    enabled: boolean,
  ) {
    if (!detail) {
      return;
    }

    const actionName = enabled ? "重新启用更新" : "停用更新";

    if (
      !window.confirm(
        `确定对设备“${device.deviceName || "未命名设备"}”执行“${actionName}”吗？\n\n` +
          "该操作只影响这台设备的在线自动更新，不影响业务软件正常启动和使用。",
      )
    ) {
      return;
    }

    try {
      setSavingDeviceId(device.id);

      const action = enabled ? "enable" : "revoke";

      const response = await apiFetch(
        `/api/client-device-admin/devices/${device.id}/${action}`,
        { method: "POST" },
      );

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `修改设备状态失败：${response.status}`);
      }

      await openDevicesByBindingId(detail.customerSoftwareId);
      await loadBindings();
    } catch (error) {
      console.error("修改设备更新权限失败：", error);
      alert(error instanceof Error ? error.message : "修改设备更新权限失败");
    } finally {
      setSavingDeviceId(null);
    }
  }

  async function openDevicesByBindingId(customerSoftwareId: number) {
    const response = await apiFetch(
      `/api/client-device-admin/bindings/${customerSoftwareId}/devices`,
    );

    if (!response.ok) {
      const text = await response.text();
      throw new Error(text || `刷新设备列表失败：${response.status}`);
    }

    setDetail((await response.json()) as DeviceDetailResult);
  }

  function formatDate(value: string | null) {
    if (!value) {
      return "-";
    }

    return new Date(value).toLocaleString("zh-CN");
  }

  function getBindingStatus(item: DeviceBindingSummary) {
    if (!item.customerEnabled) {
      return "客户已停用";
    }

    if (!item.softwareEnabled) {
      return "软件已停用";
    }

    if (!item.bindingEnabled) {
      return "授权已停用";
    }

    return "正常";
  }

  return (
    <div className="content u-page">
      <header className="u-page-header">
        <div>
          <span className="u-eyebrow">DEVICE UPDATE AUTHORIZATION</span>
          <h2>更新设备授权</h2>
          <p>
            按客户和软件查看已经完成更新授权的设备，并可单独停用或重新启用某台设备的自动更新权限。
          </p>
        </div>

        <button
          type="button"
          className="u-secondary-button"
          onClick={() => void loadBindings()}
        >
          <RefreshCw size={16} />
          刷新
        </button>
      </header>

      <section className="u-panel">
        <div
          style={{
            display: "flex",
            gap: 12,
            alignItems: "center",
            marginBottom: 18,
          }}
        >
          <Search size={17} />

          <input
            value={keyword}
            onChange={(event) => setKeyword(event.target.value)}
            placeholder="搜索客户名称、客户编码、软件名称或软件编码"
            style={{
              flex: 1,
              minWidth: 220,
              height: 38,
              border: "1px solid #d7dde8",
              borderRadius: 8,
              padding: "0 12px",
            }}
          />
        </div>

        {loading ? (
          <div className="u-loading-card">正在加载更新设备授权...</div>
        ) : errorMessage ? (
          <div className="u-error-card">
            <span>{errorMessage}</span>
            <button
              type="button"
              className="primary-button"
              onClick={() => void loadBindings()}
            >
              重新加载
            </button>
          </div>
        ) : filteredItems.length === 0 ? (
          <div className="u-empty-state">暂无符合条件的客户软件授权。</div>
        ) : (
          <div style={{ display: "grid", gap: 12 }}>
            {filteredItems.map((item) => (
              <div
                key={item.customerSoftwareId}
                style={{
                  border: "1px solid #e2e8f0",
                  borderRadius: 12,
                  padding: 16,
                  display: "grid",
                  gridTemplateColumns: "minmax(180px, 1.2fr) minmax(180px, 1.2fr) minmax(200px, 1fr) auto",
                  gap: 18,
                  alignItems: "center",
                }}
              >
                <div>
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                      fontWeight: 700,
                    }}
                  >
                    <Building2 size={16} />
                    {item.customerName}
                  </div>
                  <div style={{ color: "#64748b", fontSize: 13, marginTop: 4 }}>
                    {item.customerCode}
                  </div>
                </div>

                <div>
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: 8,
                      fontWeight: 700,
                    }}
                  >
                    <Boxes size={16} />
                    {item.softwareName}
                  </div>
                  <div style={{ color: "#64748b", fontSize: 13, marginTop: 4 }}>
                    {item.softwareCode}
                  </div>
                </div>

                <div>
                  <div style={{ fontWeight: 700 }}>
                    已授权更新设备：{item.deviceCount} 台
                  </div>
                  <div style={{ color: "#64748b", fontSize: 13, marginTop: 4 }}>
                    正常 {item.enabledDeviceCount} · 已停用 {item.disabledDeviceCount}
                  </div>
                  <div style={{ color: "#64748b", fontSize: 13, marginTop: 4 }}>
                    最近检查：{formatDate(item.lastUsedAt)}
                  </div>
                  <div style={{ color: "#64748b", fontSize: 13, marginTop: 4 }}>
                    授权状态：{getBindingStatus(item)}
                  </div>
                </div>

                <button
                  type="button"
                  className="normal-button"
                  disabled={deviceLoading}
                  onClick={() => void openDevices(item)}
                >
                  <Monitor size={15} />
                  查看设备
                </button>
              </div>
            ))}
          </div>
        )}
      </section>

      {(detail || deviceLoading) && (
        <div className="version-attachment-mask">
          <div
            className="version-attachment-dialog"
            style={{ maxWidth: 900, width: "92vw" }}
          >
            <div className="version-attachment-header">
              <div>
                <h3>更新设备管理</h3>
                {detail && (
                  <p>
                    {detail.customerName} · {detail.softwareName} ·{" "}
                    {detail.softwareCode}
                  </p>
                )}
              </div>

              <button
                type="button"
                className="normal-button"
                disabled={deviceLoading}
                onClick={() => setDetail(null)}
              >
                关闭
              </button>
            </div>

            {deviceLoading && !detail ? (
              <div className="u-loading-card">正在加载设备...</div>
            ) : detail && detail.devices.length === 0 ? (
              <div className="u-empty-state">
                当前客户的这款软件还没有任何设备完成更新授权。
              </div>
            ) : detail ? (
              <div style={{ display: "grid", gap: 12, marginTop: 16 }}>
                {detail.devices.map((device) => (
                  <div
                    key={device.id}
                    style={{
                      border: "1px solid #e2e8f0",
                      borderRadius: 10,
                      padding: 14,
                      display: "grid",
                      gridTemplateColumns: "minmax(180px, 1fr) minmax(260px, 1.5fr) auto",
                      gap: 18,
                      alignItems: "center",
                    }}
                  >
                    <div>
                      <div style={{ fontWeight: 700 }}>
                        {device.deviceName || "未命名设备"}
                      </div>
                      <div style={{ color: "#64748b", fontSize: 13, marginTop: 4 }}>
                        Token：{device.tokenPrefix || "-"}
                      </div>
                    </div>

                    <div style={{ color: "#64748b", fontSize: 13 }}>
                      <div>安装实例：{device.installationId}</div>
                      <div>授权时间：{formatDate(device.activatedAt)}</div>
                      <div>最近检查：{formatDate(device.lastUsedAt)}</div>
                    </div>

                    <div style={{ textAlign: "right" }}>
                      <div
                        style={{
                          marginBottom: 8,
                          fontWeight: 700,
                        }}
                      >
                        {device.isEnabled ? (
                          <span>
                            <ShieldCheck size={15} style={{ verticalAlign: -2 }} />{" "}
                            正常
                          </span>
                        ) : (
                          <span>
                            <ShieldOff size={15} style={{ verticalAlign: -2 }} />{" "}
                            已停用
                          </span>
                        )}
                      </div>

                      <button
                        type="button"
                        className={device.isEnabled ? "delete-button" : "normal-button"}
                        disabled={savingDeviceId === device.id}
                        onClick={() =>
                          void setDeviceEnabled(device, !device.isEnabled)
                        }
                      >
                        {savingDeviceId === device.id
                          ? "处理中..."
                          : device.isEnabled
                            ? "停用更新"
                            : "重新启用更新"}
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            ) : null}
          </div>
        </div>
      )}
    </div>
  );
}

export default ClientDeviceAdminPage;
