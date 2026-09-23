import { useEffect, useState } from "react";
import { Copy, KeyRound, Monitor, ShieldOff } from "lucide-react";
import { apiFetch } from "../services/api";

interface LatestVersion {
  id: number;
  version: string;
  title: string;
  releaseNotes: string;
  publishedAt: string | null;
  forceUpdate: boolean;
  packageFileName: string;
  packageFileSize: number;
  canDownload: boolean;
}

interface SoftwareVersionHistoryItem {
  id: number;
  version: string;
  versionType: string;
  publishStatus: string;
  title: string;
  releaseNotes: string;
  publishedAt: string | null;
  forceUpdate: boolean;
  packageFileName: string;
  packageFileSize: number;
  canDownload: boolean;
}

interface MyVersionAttachment {
  id: number;
  softwareVersionId: number;
  fileName: string;
  fileSize: number;
  contentType: string;
  attachmentType: string;
  remark: string;
  createdAt: string;
}

interface MySoftware {
  softwareId: number;
  softwareName: string;
  softwareCode: string;
  shortName: string;
  category: string;
  description: string;
  platform: string;
  boundAt: string;
  latestVersion: LatestVersion | null;
}

interface ActivationCodeResult {
  activationCode: string;
  expiresAt: string;
  softwareId: number;
  softwareCode: string;
  softwareName: string;
  message: string;
}

interface ClientDevice {
  id: number;
  installationId: string;
  deviceName: string;
  tokenPrefix: string;
  isEnabled: boolean;
  activatedAt: string;
  lastUsedAt: string | null;
  updatedAt: string;
}

function MySoftwarePage() {
  const [softwareList, setSoftwareList] = useState<MySoftware[]>([]);
  const [versionAttachments, setVersionAttachments] = useState<
    Record<number, MyVersionAttachment[]>
  >({});
  const [loading, setLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState("");

  const [historySoftware, setHistorySoftware] = useState<MySoftware | null>(null);
  const [versionHistory, setVersionHistory] = useState<
    SoftwareVersionHistoryItem[]
  >([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  const [activationResult, setActivationResult] =
    useState<ActivationCodeResult | null>(null);
  const [activationLoadingId, setActivationLoadingId] =
    useState<number | null>(null);

  const [deviceSoftware, setDeviceSoftware] = useState<MySoftware | null>(null);
  const [devices, setDevices] = useState<ClientDevice[]>([]);
  const [deviceLoading, setDeviceLoading] = useState(false);
  const [revokingDeviceId, setRevokingDeviceId] = useState<number | null>(null);

  async function loadVersionAttachments(versionId: number) {
    try {
      const response = await apiFetch(
        `/api/my-software/versions/${versionId}/attachments`,
      );

      if (!response.ok) {
        throw new Error(await response.text());
      }

      const data = (await response.json()) as MyVersionAttachment[];

      setVersionAttachments((current) => ({
        ...current,
        [versionId]: data,
      }));
    } catch (error) {
      console.error(`加载版本 ${versionId} 的资料失败：`, error);

      setVersionAttachments((current) => ({
        ...current,
        [versionId]: [],
      }));
    }
  }

  async function loadMySoftware() {
    try {
      setLoading(true);
      setErrorMessage("");

      const response = await apiFetch("/api/my-software");

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `获取我的软件失败：${response.status}`);
      }

      const data = (await response.json()) as MySoftware[];
      setSoftwareList(data);
      setVersionAttachments({});

      await Promise.all(
        data
          .filter((software) => software.latestVersion !== null)
          .map((software) =>
            loadVersionAttachments(software.latestVersion!.id),
          ),
      );
    } catch (error) {
      console.error("加载我的软件失败：", error);
      setErrorMessage("加载软件信息失败，请稍后重试。");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadMySoftware();
  }, []);

  async function openVersionHistory(software: MySoftware) {
    try {
      setHistorySoftware(software);
      setVersionHistory([]);
      setHistoryLoading(true);

      const response = await apiFetch(
        `/api/my-software/${software.softwareId}/versions`,
      );

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `获取版本历史失败：${response.status}`);
      }

      setVersionHistory(
        (await response.json()) as SoftwareVersionHistoryItem[],
      );
    } catch (error) {
      console.error("获取版本历史失败：", error);
      alert("获取版本历史失败：" + String(error));
      setHistorySoftware(null);
    } finally {
      setHistoryLoading(false);
    }
  }

  async function downloadPackage(versionId: number) {
    try {
      const response = await apiFetch(
        `/api/download/version/${versionId}/ticket`,
        { method: "POST" },
      );

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `获取下载地址失败：${response.status}`);
      }

      const data = await response.json();
      const link = document.createElement("a");
      link.href = data.downloadUrl;
      document.body.appendChild(link);
      link.click();
      link.remove();
    } catch (error) {
      console.error("下载安装包失败：", error);
      alert("下载安装包失败：" + String(error));
    }
  }

  async function downloadVersionAttachment(attachment: MyVersionAttachment) {
    try {
      const response = await apiFetch(
        `/api/my-software/attachments/${attachment.id}/download`,
      );

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `下载资料失败：${response.status}`);
      }

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = attachment.fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (error) {
      console.error("下载版本资料失败：", error);
      alert("下载版本资料失败：" + String(error));
    }
  }

  /**
   * 为当前客户的一款软件生成一次性激活码。
   *
   * 注意：
   * 这不是 UpdateToken。
   * 激活码只使用一次，真正的设备 UpdateToken
   * 会在桌面软件激活成功后由服务器返回给该设备。
   */
  async function generateActivationCode(software: MySoftware) {
    try {
      setActivationLoadingId(software.softwareId);

      const response = await apiFetch(
        `/api/client-activation/software/${software.softwareId}/code`,
        { method: "POST" },
      );

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `生成激活码失败：${response.status}`);
      }

      setActivationResult((await response.json()) as ActivationCodeResult);
    } catch (error) {
      console.error("生成激活码失败：", error);
      alert(error instanceof Error ? error.message : "生成激活码失败");
    } finally {
      setActivationLoadingId(null);
    }
  }

  async function copyActivationCode() {
    if (!activationResult) {
      return;
    }

    try {
      await navigator.clipboard.writeText(activationResult.activationCode);
      alert("激活码已复制");
    } catch {
      alert("浏览器未允许自动复制，请手工复制激活码");
    }
  }

  async function openDeviceManager(software: MySoftware) {
    setDeviceSoftware(software);
    await loadDevices(software);
  }

  async function loadDevices(software: MySoftware) {
    try {
      setDeviceLoading(true);

      const response = await apiFetch(
        `/api/client-activation/software/${software.softwareId}/devices`,
      );

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `获取设备列表失败：${response.status}`);
      }

      setDevices((await response.json()) as ClientDevice[]);
    } catch (error) {
      console.error("获取设备列表失败：", error);
      alert(error instanceof Error ? error.message : "获取设备列表失败");
      setDevices([]);
    } finally {
      setDeviceLoading(false);
    }
  }

  async function revokeDevice(device: ClientDevice) {
    if (!deviceSoftware) {
      return;
    }

    if (
      !window.confirm(
        `确定停用设备“${device.deviceName}”的自动更新权限吗？\n\n只会影响这一台设备，其他设备不受影响。`,
      )
    ) {
      return;
    }

    try {
      setRevokingDeviceId(device.id);

      const response = await apiFetch(
        `/api/client-activation/devices/${device.id}/revoke`,
        { method: "POST" },
      );

      if (!response.ok) {
        const text = await response.text();
        throw new Error(text || `停用设备失败：${response.status}`);
      }

      await loadDevices(deviceSoftware);
    } catch (error) {
      console.error("停用设备失败：", error);
      alert(error instanceof Error ? error.message : "停用设备失败");
    } finally {
      setRevokingDeviceId(null);
    }
  }

  function getAttachmentTypeName(type: string) {
    switch (type) {
      case "Manual":
        return "用户手册";
      case "ReleaseDocument":
        return "版本说明";
      case "Troubleshooting":
        return "常见问题";
      case "Config":
        return "配置文件";
      case "Other":
        return "其他资料";
      default:
        return "资料";
    }
  }

  function getVersionTypeName(versionType: string) {
    switch (versionType) {
      case "Release":
        return "正式版";
      case "Beta":
        return "测试版";
      case "Dev":
        return "开发版";
      default:
        return versionType;
    }
  }

  function getPublishStatusName(status: string) {
    switch (status) {
      case "Published":
        return "已发布";
      case "Deprecated":
        return "已停用";
      case "Draft":
        return "草稿";
      default:
        return status;
    }
  }

  function formatFileSize(bytes: number) {
    if (bytes <= 0) return "-";
    const kb = bytes / 1024;
    if (kb < 1024) return `${kb.toFixed(1)} KB`;
    const mb = kb / 1024;
    if (mb < 1024) return `${mb.toFixed(1)} MB`;
    return `${(mb / 1024).toFixed(2)} GB`;
  }

  function formatDate(dateText: string | null) {
    if (!dateText) return "-";
    return new Date(dateText).toLocaleString("zh-CN");
  }

  return (
    <div className="content">
      <div className="title-row">
        <h2>我的软件</h2>
      </div>

      {loading && (
        <div className="my-software-message">正在加载您的软件...</div>
      )}

      {!loading && errorMessage && (
        <div className="my-software-error">
          {errorMessage}
          <button className="normal-button" onClick={() => void loadMySoftware()}>
            重新加载
          </button>
        </div>
      )}

      {!loading && !errorMessage && softwareList.length === 0 && (
        <div className="my-software-empty">
          <div className="my-software-empty-title">暂无授权软件</div>
          <div className="my-software-empty-text">
            当前账号所属客户暂未绑定任何软件，请联系软件服务人员进行授权。
          </div>
        </div>
      )}

      {!loading && !errorMessage && softwareList.length > 0 && (
        <div className="my-software-grid">
          {softwareList.map((software) => (
            <div className="my-software-card" key={software.softwareId}>
              <div className="my-software-card-header">
                <div>
                  <div className="my-software-name">{software.softwareName}</div>
                  <div className="my-software-code">{software.softwareCode}</div>
                </div>
                {software.category && (
                  <span className="my-software-category">{software.category}</span>
                )}
              </div>

              <div className="my-software-description">
                {software.description || "暂无软件说明"}
              </div>

              <div className="my-software-info">
                <div>
                  <span>软件简称</span>
                  <strong>{software.shortName || "-"}</strong>
                </div>
                <div>
                  <span>运行平台</span>
                  <strong>{software.platform || "-"}</strong>
                </div>
                <div>
                  <span>授权时间</span>
                  <strong>{formatDate(software.boundAt)}</strong>
                </div>
              </div>

              {/*
               * 设备激活与软件下载分开。
               * 下载完整安装包并不代表每次都要重新申请激活码；
               * 只有一台新设备第一次安装时才需要激活一次。
               */}
              <div
                className="my-version-actions"
                style={{ marginBottom: 16, flexWrap: "wrap" }}
              >
                <button
                  type="button"
                  className="normal-button"
                  disabled={activationLoadingId === software.softwareId}
                  onClick={() => void generateActivationCode(software)}
                >
                  <KeyRound size={14} />
                  {activationLoadingId === software.softwareId
                    ? "生成中..."
                    : "获取激活码"}
                </button>

                <button
                  type="button"
                  className="normal-button"
                  onClick={() => void openDeviceManager(software)}
                >
                  <Monitor size={14} />
                  设备管理
                </button>
              </div>

              <div className="my-version-area">
                {software.latestVersion === null ? (
                  <div className="my-version-empty">
                    当前暂无可用版本
                    <div style={{ marginTop: 12 }}>
                      <button
                        type="button"
                        className="normal-button"
                        onClick={() => void openVersionHistory(software)}
                      >
                        查看历史版本
                      </button>
                    </div>
                  </div>
                ) : (
                  <>
                    <div className="my-version-header">
                      <div>
                        <span className="my-version-label">最新版本</span>
                        <strong className="my-version-number">
                          {software.latestVersion.version}
                        </strong>
                      </div>

                      {software.latestVersion.forceUpdate && (
                        <span className="force-update-badge">强制升级</span>
                      )}
                    </div>

                    <div className="my-version-title">
                      {software.latestVersion.title || "正式版本"}
                    </div>

                    <div className="my-version-notes">
                      {software.latestVersion.releaseNotes || "暂无更新说明"}
                    </div>

                    <div className="my-version-meta">
                      <span>
                        发布时间：{formatDate(software.latestVersion.publishedAt)}
                      </span>
                      {software.latestVersion.packageFileName && (
                        <span>安装包：{software.latestVersion.packageFileName}</span>
                      )}
                      {software.latestVersion.packageFileSize > 0 && (
                        <span>
                          大小：{formatFileSize(software.latestVersion.packageFileSize)}
                        </span>
                      )}
                    </div>

                    <div className="my-version-actions">
                      <button
                        type="button"
                        className="primary-button"
                        disabled={!software.latestVersion.canDownload}
                        onClick={() => downloadPackage(software.latestVersion!.id)}
                      >
                        {software.latestVersion.canDownload
                          ? "下载安装包"
                          : "暂不可下载"}
                      </button>

                      <button
                        type="button"
                        className="normal-button"
                        onClick={() => void openVersionHistory(software)}
                      >
                        历史版本
                      </button>
                    </div>

                    <div className="my-version-resources">
                      <div className="my-version-resources-title">版本资料</div>

                      {(versionAttachments[software.latestVersion.id] ?? [])
                        .length === 0 ? (
                        <div className="my-version-resources-empty">
                          暂无可下载资料
                        </div>
                      ) : (
                        <div className="my-version-resources-list">
                          {(versionAttachments[software.latestVersion.id] ?? []).map(
                            (attachment) => (
                              <div
                                className="my-version-resource-item"
                                key={attachment.id}
                              >
                                <div className="my-version-resource-info">
                                  <div className="my-version-resource-name">
                                    {attachment.fileName}
                                  </div>
                                  <div className="my-version-resource-meta">
                                    <span>
                                      {getAttachmentTypeName(
                                        attachment.attachmentType,
                                      )}
                                    </span>
                                    <span>{formatFileSize(attachment.fileSize)}</span>
                                    {attachment.remark && (
                                      <span>{attachment.remark}</span>
                                    )}
                                  </div>
                                </div>

                                <button
                                  type="button"
                                  className="normal-button"
                                  onClick={() =>
                                    void downloadVersionAttachment(attachment)
                                  }
                                >
                                  下载
                                </button>
                              </div>
                            ),
                          )}
                        </div>
                      )}
                    </div>
                  </>
                )}
              </div>
            </div>
          ))}
        </div>
      )}

      {activationResult && (
        <div className="version-attachment-mask">
          <div className="version-attachment-dialog" style={{ maxWidth: 560 }}>
            <div className="version-attachment-header">
              <div>
                <h3>一次性激活码</h3>
                <p>
                  {activationResult.softwareName} · {activationResult.softwareCode}
                </p>
              </div>
              <button
                type="button"
                className="normal-button"
                onClick={() => setActivationResult(null)}
              >
                关闭
              </button>
            </div>

            <div
              style={{
                padding: 12,
                borderRadius: 8,
                background: "#fff7ed",
                color: "#9a3412",
                marginTop: 12,
              }}
            >
              激活码只用于一台新设备首次激活，成功使用后立即失效。它不是长期 UpdateToken。
            </div>

            <div
              style={{
                margin: "20px 0",
                padding: 18,
                border: "1px dashed #94a3b8",
                borderRadius: 10,
                textAlign: "center",
              }}
            >
              <div style={{ fontSize: 26, fontWeight: 700, letterSpacing: 2 }}>
                {activationResult.activationCode}
              </div>
              <div style={{ marginTop: 8, color: "#64748b" }}>
                有效期至：{formatDate(activationResult.expiresAt)}
              </div>
            </div>

            <div className="form-buttons">
              <button
                type="button"
                className="primary-button"
                onClick={() => void copyActivationCode()}
              >
                <Copy size={15} />
                复制激活码
              </button>
              <button
                type="button"
                className="normal-button"
                onClick={() => setActivationResult(null)}
              >
                已保存，关闭
              </button>
            </div>
          </div>
        </div>
      )}

      {deviceSoftware && (
        <div className="version-attachment-mask">
          <div className="version-attachment-dialog" style={{ maxWidth: 760 }}>
            <div className="version-attachment-header">
              <div>
                <h3>设备管理</h3>
                <p>
                  {deviceSoftware.softwareName} · {deviceSoftware.softwareCode}
                </p>
              </div>
              <button
                type="button"
                className="normal-button"
                onClick={() => {
                  setDeviceSoftware(null);
                  setDevices([]);
                }}
              >
                关闭
              </button>
            </div>

            {deviceLoading ? (
              <div className="my-software-message">正在加载设备...</div>
            ) : devices.length === 0 ? (
              <div className="my-software-empty">
                暂无已激活设备。新电脑安装软件后，使用一次性激活码完成首次激活。
              </div>
            ) : (
              <div style={{ display: "grid", gap: 12 }}>
                {devices.map((device) => (
                  <div
                    key={device.id}
                    style={{
                      border: "1px solid #e2e8f0",
                      borderRadius: 10,
                      padding: 14,
                      display: "flex",
                      justifyContent: "space-between",
                      gap: 18,
                      alignItems: "center",
                    }}
                  >
                    <div style={{ minWidth: 0 }}>
                      <div style={{ fontWeight: 700 }}>
                        {device.deviceName || "未命名设备"}
                      </div>
                      <div style={{ color: "#64748b", fontSize: 13 }}>
                        安装实例：{device.installationId}
                      </div>
                      <div style={{ color: "#64748b", fontSize: 13 }}>
                        激活：{formatDate(device.activatedAt)} · 最近检查：
                        {formatDate(device.lastUsedAt)}
                      </div>
                      <div style={{ color: "#64748b", fontSize: 13 }}>
                        Token：{device.tokenPrefix}
                      </div>
                    </div>

                    <div style={{ textAlign: "right" }}>
                      <div style={{ marginBottom: 8 }}>
                        {device.isEnabled ? "正常" : "已停用"}
                      </div>

                      {device.isEnabled && (
                        <button
                          type="button"
                          className="delete-button"
                          disabled={revokingDeviceId === device.id}
                          onClick={() => void revokeDevice(device)}
                        >
                          <ShieldOff size={14} />
                          {revokingDeviceId === device.id ? "处理中..." : "停用"}
                        </button>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {historySoftware && (
        <div className="version-attachment-mask">
          <div className="version-attachment-dialog">
            <div className="version-attachment-header">
              <div>
                <h3>历史版本</h3>
                <p>
                  {historySoftware.softwareName} · {historySoftware.softwareCode}
                </p>
              </div>
              <button
                type="button"
                className="normal-button"
                onClick={() => setHistorySoftware(null)}
              >
                关闭
              </button>
            </div>

            {historyLoading ? (
              <div className="my-software-message">正在加载历史版本...</div>
            ) : versionHistory.length === 0 ? (
              <div className="my-software-empty">暂无历史版本</div>
            ) : (
              <div>
                {versionHistory.map((version) => (
                  <div key={version.id} className="my-version-history-item">
                    <div className="my-version-history-header">
                      <div>
                        <strong>{version.version}</strong>
                        <span> {getVersionTypeName(version.versionType)}</span>
                      </div>
                      <span>{getPublishStatusName(version.publishStatus)}</span>
                    </div>

                    <div>{version.title || "暂无版本标题"}</div>
                    <div className="my-version-notes">
                      {version.releaseNotes || "暂无更新说明"}
                    </div>
                    <div className="my-version-meta">
                      <span>发布时间：{formatDate(version.publishedAt)}</span>
                      {version.packageFileName && (
                        <span>安装包：{version.packageFileName}</span>
                      )}
                      {version.packageFileSize > 0 && (
                        <span>大小：{formatFileSize(version.packageFileSize)}</span>
                      )}
                    </div>

                    <div className="my-version-actions">
                      <button
                        type="button"
                        className="primary-button"
                        disabled={!version.canDownload}
                        onClick={() => downloadPackage(version.id)}
                      >
                        {version.canDownload
                          ? "下载安装包"
                          : version.publishStatus === "Deprecated"
                            ? "版本已停用"
                            : "暂不可下载"}
                      </button>

                      {version.forceUpdate && (
                        <span className="force-update-badge">强制升级</span>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default MySoftwarePage;
