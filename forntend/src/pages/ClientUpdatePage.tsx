import { useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  FileArchive,
  RefreshCw,
  UploadCloud,
} from "lucide-react";

import { apiFetch } from "../services/api";
import "../styles/client-update.css";

interface UpdatePackageItem {
  versionId: number;
  softwareId: number;
  softwareName: string;
  softwareCode: string;
  version: string;
  versionType: string;
  publishStatus: string;
  hasUpdatePackage: boolean;
  fileCount: number;
  totalFileSize: number;
  deleteCount: number;
  generatedAt: string | null;
  fullPackageAvailable: boolean;
  fullPackageFileName: string;
  fullPackageFileSize: number;
}

interface PublishCustomer {
  customerId: number;
  name: string;
  code: string;
  province: string;
  city: string;
}

interface UpdateManifest {
  schemaVersion: number;
  softwareId: number;
  versionId: number;
  version: string;
  generatedAt: string;
  totalFileSize: number;
  files: Array<{
    path: string;
    size: number;
    sha256: string;
  }>;
  deletePaths: string[];
}

interface Preflight {
  versionId: number;
  softwareName: string;
  softwareCode: string;
  version: string;
  versionType: string;
  hasFullPackage: boolean;
  hasUpdatePackage: boolean;
  manifestFileCount: number;
  manifestTotalFileSize: number;
  manifestDeleteCount: number;
  containsUpdaterSelfUpdate: boolean;
  authorizedCustomerCount: number;
  errors: string[];
  warnings: string[];
  canPublish: boolean;
}

function ClientUpdatePage() {
  const [items, setItems] = useState<UpdatePackageItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [keyword, setKeyword] = useState("");
  const [errorMessage, setErrorMessage] = useState("");

  const [uploadItem, setUploadItem] = useState<UpdatePackageItem | null>(null);
  const [zipFile, setZipFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);

  const [manifest, setManifest] = useState<UpdateManifest | null>(null);

  const [publishing, setPublishing] = useState<UpdatePackageItem | null>(null);
  const [preflight, setPreflight] = useState<Preflight | null>(null);
  const [customers, setCustomers] = useState<PublishCustomer[]>([]);
  const [publishToAll, setPublishToAll] = useState(true);
  const [selectedCustomerIds, setSelectedCustomerIds] = useState<number[]>([]);
  const [isPublishing, setIsPublishing] = useState(false);

  async function load() {
    try {
      setLoading(true);
      setErrorMessage("");

      const response = await apiFetch("/api/software-version-update-packages");

      if (!response.ok) {
        throw new Error((await response.text()) || "加载自动更新包失败");
      }

      setItems((await response.json()) as UpdatePackageItem[]);
    } catch (error) {
      setErrorMessage(
        error instanceof Error ? error.message : "加载自动更新包失败",
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  const filtered = useMemo(() => {
    const value = keyword.trim().toLowerCase();

    if (!value) {
      return items;
    }

    return items.filter(
      (item) =>
        item.softwareName.toLowerCase().includes(value) ||
        item.softwareCode.toLowerCase().includes(value) ||
        item.version.toLowerCase().includes(value),
    );
  }, [items, keyword]);

  function formatSize(bytes: number) {
    if (!bytes) {
      return "-";
    }

    const mb = bytes / 1024 / 1024;

    return mb < 1024
      ? `${mb.toFixed(1)} MB`
      : `${(mb / 1024).toFixed(2)} GB`;
  }

  function uploadWithProgress(versionId: number, file: File) {
    return new Promise<void>((resolve, reject) => {
      const xhr = new XMLHttpRequest();
      const form = new FormData();

      form.append("file", file);

      xhr.open(
        "POST",
        `/api/software-version-update-packages/${versionId}`,
      );

      const token = sessionStorage.getItem("access_token");

      if (token) {
        xhr.setRequestHeader(
          "Authorization",
          `Bearer ${token}`,
        );
      }

      xhr.upload.onprogress = (event) => {
        if (!event.lengthComputable) {
          return;
        }

        setUploadProgress(
          Math.round(
            (event.loaded / event.total) * 100,
          ),
        );
      };

      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          resolve();
          return;
        }

        reject(
          new Error(
            xhr.responseText ||
              `上传失败：${xhr.status}`,
          ),
        );
      };

      xhr.onerror = () =>
        reject(
          new Error(
            "网络错误，更新 ZIP 上传失败",
          ),
        );

      xhr.send(form);
    });
  }

  async function uploadPackage() {
    if (!uploadItem || !zipFile) {
      return;
    }

    try {
      setUploading(true);
      setUploadProgress(0);

      await uploadWithProgress(
        uploadItem.versionId,
        zipFile,
      );

      alert("自动更新包生成成功");

      setUploadItem(null);
      setZipFile(null);

      await load();
    } catch (error) {
      alert(
        error instanceof Error
          ? error.message
          : "上传失败",
      );
    } finally {
      setUploading(false);
    }
  }

  async function openManifest(item: UpdatePackageItem) {
    try {
      const response = await apiFetch(
        `/api/software-version-update-packages/${item.versionId}`,
      );

      if (!response.ok) {
        throw new Error(
          (await response.text()) ||
            "读取文件清单失败",
        );
      }

      setManifest(
        (await response.json()) as UpdateManifest,
      );
    } catch (error) {
      alert(
        error instanceof Error
          ? error.message
          : "读取文件清单失败",
      );
    }
  }

  async function deletePackage(item: UpdatePackageItem) {
    if (
      !window.confirm(
        `确定删除 ${item.softwareName} ${item.version} 的自动更新包吗？`,
      )
    ) {
      return;
    }

    const response = await apiFetch(
      `/api/software-version-update-packages/${item.versionId}`,
      {
        method: "DELETE",
      },
    );

    if (!response.ok) {
      alert(
        (await response.text()) ||
          "删除更新包失败",
      );
      return;
    }

    await load();
  }

  async function openPublish(item: UpdatePackageItem) {
    try {
      const [checkResponse, customerResponse] =
        await Promise.all([
          apiFetch(
            `/api/release-preflight/${item.versionId}`,
          ),
          apiFetch(
            `/api/softwareversions/${item.versionId}/publish-customers`,
          ),
        ]);

      if (!checkResponse.ok) {
        throw new Error(
          (await checkResponse.text()) ||
            "发布前检查失败",
        );
      }

      if (!customerResponse.ok) {
        throw new Error(
          (await customerResponse.text()) ||
            "读取发布客户失败",
        );
      }

      const check =
        (await checkResponse.json()) as Preflight;

      setPreflight(check);

      if (!check.canPublish) {
        alert(
          "发布前检查未通过：\n\n"
            + check.errors.join("\n"),
        );
        return;
      }

      setCustomers(
        (await customerResponse.json()) as
          PublishCustomer[],
      );

      setPublishing(item);

      setPublishToAll(
        item.versionType !== "Beta",
      );

      setSelectedCustomerIds([]);
    } catch (error) {
      alert(
        error instanceof Error
          ? error.message
          : "发布前检查失败",
      );
    }
  }

  function toggleCustomer(id: number) {
    setSelectedCustomerIds((current) =>
      current.includes(id)
        ? current.filter(
            (value) => value !== id,
          )
        : [...current, id],
    );
  }

  async function confirmPublish() {
    if (!publishing || !preflight) {
      return;
    }

    if (
      !publishToAll
      && selectedCustomerIds.length === 0
    ) {
      alert("请选择至少一个发布客户");
      return;
    }

    const customerCount =
      publishToAll
        ? customers.length
        : selectedCustomerIds.length;

    const warningText =
      preflight.warnings.length > 0
        ? "\n\n警告：\n"
          + preflight.warnings.join("\n")
        : "";

    if (
      !window.confirm(
        `即将发布：\n`
          + `${publishing.softwareName} ${publishing.version}\n`
          + `影响客户：${customerCount} 家\n`
          + `更新文件：${preflight.manifestFileCount} 个`
          + warningText
          + "\n\n确认发布吗？",
      )
    ) {
      return;
    }

    try {
      setIsPublishing(true);

      const response = await apiFetch(
        `/api/softwareversions/${publishing.versionId}/publish`,
        {
          method: "POST",
          headers: {
            "Content-Type":
              "application/json",
          },
          body: JSON.stringify({
            publishToAll,
            customerIds:
              selectedCustomerIds,
          }),
        },
      );

      if (!response.ok) {
        const text =
          await response.text();

        let message = text;

        try {
          const json = JSON.parse(text) as {
            message?: string;
            errors?: string[];
          };

          if (
            json.errors
            && json.errors.length > 0
          ) {
            message =
              `${json.message || "发布失败"}：\n`
              + json.errors.join("\n");
          }
        } catch {
          // 普通字符串响应直接显示。
        }

        throw new Error(
          message || "发布失败",
        );
      }

      alert(
        `${publishing.softwareName} ${publishing.version} 发布成功`,
      );

      setPublishing(null);
      setPreflight(null);
      setCustomers([]);
      setSelectedCustomerIds([]);

      await load();
    } catch (error) {
      alert(
        error instanceof Error
          ? error.message
          : "发布失败",
      );
    } finally {
      setIsPublishing(false);
    }
  }

  return (
    <div className="content u-page">
      <header className="u-page-header">
        <div>
          <span className="u-eyebrow">
            CLIENT UPDATE
          </span>

          <h2>客户端更新</h2>

          <p>
            管理目标版本更新 ZIP、查看文件清单并发布版本。
            旧版共享 UpdateToken 已停用，设备授权请使用“更新设备授权”。
          </p>
        </div>

        <button
          type="button"
          className="normal-button"
          onClick={() => void load()}
        >
          <RefreshCw size={16} />
          刷新
        </button>
      </header>

      <section className="u-panel">
        <input
          value={keyword}
          onChange={(event) =>
            setKeyword(event.target.value)
          }
          placeholder="搜索软件或版本"
          style={{
            width: "100%",
            maxWidth: 480,
            height: 38,
            padding: "0 12px",
            marginBottom: 16,
          }}
        />

        {loading ? (
          <div>正在加载...</div>
        ) : errorMessage ? (
          <div className="u-error-card">
            {errorMessage}
          </div>
        ) : (
          <div
            style={{
              display: "grid",
              gap: 12,
            }}
          >
            {filtered.map((item) => (
              <div
                key={item.versionId}
                style={{
                  border:
                    "1px solid #e2e8f0",
                  borderRadius: 12,
                  padding: 16,
                  display: "grid",
                  gridTemplateColumns:
                    "minmax(240px,1.5fr) minmax(220px,1fr) auto",
                  gap: 18,
                  alignItems: "center",
                }}
              >
                <div>
                  <strong>
                    {item.softwareName} ·{" "}
                    {item.version}
                  </strong>

                  <div
                    style={{
                      fontSize: 13,
                      color: "#64748b",
                      marginTop: 5,
                    }}
                  >
                    {item.softwareCode} ·{" "}
                    {item.versionType} ·{" "}
                    {item.publishStatus}
                  </div>
                </div>

                <div
                  style={{
                    fontSize: 13,
                    lineHeight: 1.8,
                  }}
                >
                  <div>
                    更新 ZIP：
                    {item.hasUpdatePackage
                      ? `${item.fileCount} 文件 / ${formatSize(item.totalFileSize)}`
                      : "未上传"}
                  </div>

                  <div>
                    完整安装包：
                    {item.fullPackageAvailable
                      ? item.fullPackageFileName
                        || "已上传"
                      : "未上传"}
                  </div>
                </div>

                <div
                  style={{
                    display: "flex",
                    gap: 8,
                    flexWrap: "wrap",
                  }}
                >
                  {item.publishStatus ===
                    "Draft"
                    && item.versionType
                      !== "Dev" && (
                    <button
                      type="button"
                      className="normal-button"
                      onClick={() => {
                        setUploadItem(item);
                        setZipFile(null);
                        setUploadProgress(0);
                      }}
                    >
                      <UploadCloud
                        size={15}
                      />
                      上传更新 ZIP
                    </button>
                  )}

                  {item.hasUpdatePackage && (
                    <button
                      type="button"
                      className="normal-button"
                      onClick={() =>
                        void openManifest(
                          item,
                        )
                      }
                    >
                      <FileArchive
                        size={15}
                      />
                      查看清单
                    </button>
                  )}

                  {item.hasUpdatePackage
                    && item.publishStatus
                      === "Draft" && (
                    <button
                      type="button"
                      className="delete-button"
                      onClick={() =>
                        void deletePackage(
                          item,
                        )
                      }
                    >
                      删除更新包
                    </button>
                  )}

                  {item.publishStatus ===
                    "Draft"
                    && item.versionType
                      !== "Dev" && (
                    <button
                      type="button"
                      className="primary-button"
                      onClick={() =>
                        void openPublish(
                          item,
                        )
                      }
                    >
                      <CheckCircle2
                        size={15}
                      />
                      发布前检查
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      {uploadItem && (
        <div className="version-attachment-mask">
          <div
            className="version-attachment-dialog"
            style={{ maxWidth: 600 }}
          >
            <h3>
              上传自动更新 ZIP ·{" "}
              {uploadItem.softwareName}{" "}
              {uploadItem.version}
            </h3>

            <p>
              ZIP 应包含目标版本完整运行目录。
              如需同时升级更新器，把新版
              <code>
                {" "}
                SoftwareServicePlatform.Updater.exe{" "}
              </code>
              放入
              <code>
                {" "}
                .updater-self/{" "}
              </code>
              目录。
            </p>

            <input
              type="file"
              accept=".zip,application/zip"
              onChange={(event) =>
                setZipFile(
                  event.target.files?.[0]
                    ?? null,
                )
              }
            />

            {uploading && (
              <p>
                上传进度：
                {uploadProgress}%
              </p>
            )}

            <div
              className="form-buttons"
              style={{ marginTop: 18 }}
            >
              <button
                type="button"
                className="primary-button"
                disabled={
                  !zipFile || uploading
                }
                onClick={() =>
                  void uploadPackage()
                }
              >
                {uploading
                  ? "上传中..."
                  : "上传并生成更新包"}
              </button>

              <button
                type="button"
                className="normal-button"
                disabled={uploading}
                onClick={() =>
                  setUploadItem(null)
                }
              >
                取消
              </button>
            </div>
          </div>
        </div>
      )}

      {manifest && (
        <div className="version-attachment-mask">
          <div
            className="version-attachment-dialog"
            style={{
              maxWidth: 900,
              width: "92vw",
            }}
          >
            <div className="version-attachment-header">
              <div>
                <h3>
                  目标版本文件清单
                </h3>
                <p>
                  {manifest.version} ·{" "}
                  {manifest.files.length}{" "}
                  文件 ·{" "}
                  {formatSize(
                    manifest.totalFileSize,
                  )}
                </p>
              </div>

              <button
                type="button"
                className="normal-button"
                onClick={() =>
                  setManifest(null)
                }
              >
                关闭
              </button>
            </div>

            <div
              style={{
                maxHeight: "60vh",
                overflow: "auto",
                marginTop: 14,
              }}
            >
              {manifest.files.map(
                (file) => (
                  <div
                    key={file.path}
                    style={{
                      borderBottom:
                        "1px solid #eef2f7",
                      padding: "8px 0",
                    }}
                  >
                    <strong>
                      {file.path}
                    </strong>

                    <small
                      style={{
                        marginLeft: 12,
                      }}
                    >
                      {formatSize(
                        file.size,
                      )}
                    </small>
                  </div>
                ),
              )}
            </div>
          </div>
        </div>
      )}

      {publishing && preflight && (
        <div className="version-attachment-mask">
          <div
            className="version-attachment-dialog"
            style={{ maxWidth: 760 }}
          >
            <h3>
              发布版本 ·{" "}
              {publishing.softwareName}{" "}
              {publishing.version}
            </h3>

            <div
              style={{
                padding: 12,
                border:
                  "1px solid #cbd5e1",
                borderRadius: 8,
                marginBottom: 14,
              }}
            >
              <div>
                完整安装包：
                {preflight.hasFullPackage
                  ? "✅"
                  : "—"}{" "}
                · 自动更新包：
                {preflight.hasUpdatePackage
                  ? "✅"
                  : "—"}
              </div>

              <div>
                更新清单：
                {preflight.manifestFileCount}
                个文件 · 删除{" "}
                {preflight.manifestDeleteCount}
                个
              </div>

              <div>
                Updater 自更新：
                {preflight.containsUpdaterSelfUpdate
                  ? "本版本包含"
                  : "本版本不包含"}
              </div>

              {preflight.warnings.map(
                (warning) => (
                  <div
                    key={warning}
                    style={{ marginTop: 6 }}
                  >
                    <AlertTriangle
                      size={14}
                    />{" "}
                    {warning}
                  </div>
                ),
              )}
            </div>

            <label>
              <input
                type="radio"
                checked={publishToAll}
                disabled={
                  publishing.versionType
                  === "Beta"
                }
                onChange={() =>
                  setPublishToAll(true)
                }
              />{" "}
              全部授权客户（
              {customers.length} 家）
            </label>

            <br />

            <label>
              <input
                type="radio"
                checked={!publishToAll}
                onChange={() =>
                  setPublishToAll(false)
                }
              />{" "}
              指定客户
            </label>

            {!publishToAll && (
              <div
                style={{
                  marginTop: 12,
                  maxHeight: 260,
                  overflow: "auto",
                }}
              >
                {customers.map(
                  (customer) => (
                    <label
                      key={
                        customer.customerId
                      }
                      style={{
                        display:
                          "block",
                        marginBottom: 8,
                      }}
                    >
                      <input
                        type="checkbox"
                        checked={selectedCustomerIds.includes(
                          customer.customerId,
                        )}
                        onChange={() =>
                          toggleCustomer(
                            customer.customerId,
                          )
                        }
                      />{" "}
                      {customer.name} ·{" "}
                      {customer.code}
                    </label>
                  ),
                )}
              </div>
            )}

            <div
              className="form-buttons"
              style={{ marginTop: 18 }}
            >
              <button
                type="button"
                className="primary-button"
                disabled={isPublishing}
                onClick={() =>
                  void confirmPublish()
                }
              >
                {isPublishing
                  ? "发布中..."
                  : "确认发布"}
              </button>

              <button
                type="button"
                className="normal-button"
                disabled={isPublishing}
                onClick={() => {
                  setPublishing(null);
                  setPreflight(null);
                }}
              >
                取消
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default ClientUpdatePage;
