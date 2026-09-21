import { useEffect, useMemo, useState } from "react";
import {
  CheckCircle2,
  Copy,
  FileArchive,
  KeyRound,
  RefreshCw,
  ShieldCheck,
  UploadCloud,
} from "lucide-react";

import { apiFetch } from "../services/api";
import { getSessionUser } from "../utils/session";
import "../styles/client-update.css";

interface UpdateBindingItem {
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

  credentialExists: boolean;
  credentialEnabled: boolean;

  tokenPrefix: string | null;
  tokenCreatedAt: string | null;
  tokenUpdatedAt: string | null;
  lastUsedAt: string | null;
}

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
interface GeneratedTokenResult {
  message: string;

  updateToken: string;

  bindingId: number;

  customerName: string;

  softwareName: string;

  softwareCode: string;
}

interface UpdateManifestFile {
  path: string;

  size: number;

  sha256: string;
}

interface UpdateManifest {
  schemaVersion: number;

  softwareId: number;

  versionId: number;

  version: string;

  generatedAt: string;

  totalFileSize: number;

  files: UpdateManifestFile[];

  deletePaths: string[];
}


/**
 * 客户端自动更新管理。
 *
 * Admin：
 * - 客户 UpdateToken 管理
 * - 增量更新包管理
 *
 * Developer：
 * - 增量更新包管理
 *
 * 客户端实际检查更新不经过这个页面，
 * 而是调用 /api/client-updates/*
 */
function ClientUpdatePage() {
  const currentUser = getSessionUser();

  const isAdmin = currentUser?.role === "Admin";

  const [bindingItems, setBindingItems] = useState<UpdateBindingItem[]>([]);

  const [packageItems, setPackageItems] = useState<UpdatePackageItem[]>([]);

  const [loading, setLoading] = useState(true);

  const [errorMessage, setErrorMessage] = useState("");

  const [bindingKeyword, setBindingKeyword] = useState("");

  const [packageKeyword, setPackageKeyword] = useState("");

  const [generatedToken, setGeneratedToken] =
    useState<GeneratedTokenResult | null>(null);

  const [savingBindingId, setSavingBindingId] =
    useState<number | null>(null);

  const [selectedPackageVersion, setSelectedPackageVersion] =
    useState<UpdatePackageItem | null>(null);

  const [selectedZip, setSelectedZip] =
    useState<File | null>(null);

  const [uploadingPackage, setUploadingPackage] =
    useState(false);

  /*
   * ZIP 从浏览器发送到服务器的上传进度。
   *
   * 100% 只代表“浏览器已经发送完”，
   * 服务端随后还需要解压 ZIP、复制文件并计算 SHA256。
   */
  const [uploadProgress, setUploadProgress] =
    useState(0);

  const [serverProcessing, setServerProcessing] =
    useState(false);

  const [manifest, setManifest] =
    useState<UpdateManifest | null>(null);

  const [manifestLoading, setManifestLoading] =
    useState(false);

const [publishingVersion, setPublishingVersion] =
  useState<UpdatePackageItem | null>(null);

const [publishCustomers, setPublishCustomers] =
  useState<PublishCustomer[]>([]);

const [publishToAll, setPublishToAll] =
  useState(true);

const [selectedCustomerIds, setSelectedCustomerIds] =
  useState<number[]>([]);

const [isPublishing, setIsPublishing] =
  useState(false);
  async function loadData() {
    try {
      setLoading(true);

      setErrorMessage("");

      const requests: Promise<Response>[] = [
        apiFetch(
          "/api/software-version-update-packages",
        ),
      ];

      /*
       * Developer 不允许查看客户更新 Token。
       */
      if (isAdmin) {
        requests.push(
          apiFetch(
            "/api/client-update-admin/bindings",
          ),
        );
      }

      const responses =
        await Promise.all(
          requests,
        );


      const packageResponse =
        responses[0];

      if (!packageResponse.ok) {
        const text =
          await packageResponse.text();

        throw new Error(
          text ||
            `加载自动更新包失败：${packageResponse.status}`,
        );
      }

      setPackageItems(
        (await packageResponse.json()) as
          UpdatePackageItem[],
      );


      if (isAdmin) {
        const bindingResponse =
          responses[1];

        if (!bindingResponse.ok) {
          const text =
            await bindingResponse.text();

          throw new Error(
            text ||
              `加载更新凭证失败：${bindingResponse.status}`,
          );
        }

        setBindingItems(
          (await bindingResponse.json()) as
            UpdateBindingItem[],
        );
      } else {
        setBindingItems([]);
      }
    } catch (error) {
      console.error(
        "加载客户端自动更新配置失败：",
        error,
      );

      setErrorMessage(
        error instanceof Error
          ? error.message
          : "加载客户端自动更新配置失败",
      );
    } finally {
      setLoading(false);
    }
  }


  useEffect(() => {
    void loadData();
  }, []);


  const filteredBindings =
    useMemo(() => {
      const keyword =
        bindingKeyword
          .trim()
          .toLowerCase();

      if (!keyword) {
        return bindingItems;
      }

      return bindingItems.filter(
        (item) =>
          item.customerName
            .toLowerCase()
            .includes(keyword)
          ||
          item.customerCode
            .toLowerCase()
            .includes(keyword)
          ||
          item.softwareName
            .toLowerCase()
            .includes(keyword)
          ||
          item.softwareCode
            .toLowerCase()
            .includes(keyword),
      );
    }, [
      bindingItems,
      bindingKeyword,
    ]);


  const filteredPackages =
    useMemo(() => {
      const keyword =
        packageKeyword
          .trim()
          .toLowerCase();

      if (!keyword) {
        return packageItems;
      }

      return packageItems.filter(
        (item) =>
          item.softwareName
            .toLowerCase()
            .includes(keyword)
          ||
          item.softwareCode
            .toLowerCase()
            .includes(keyword)
          ||
          item.version
            .toLowerCase()
            .includes(keyword)
          ||
          item.publishStatus
            .toLowerCase()
            .includes(keyword),
      );
    }, [
      packageItems,
      packageKeyword,
    ]);


  async function generateToken(
    item: UpdateBindingItem,
  ) {
    const reset =
      item.credentialExists;

    if (
      reset
      &&
      !window.confirm(
        `确定重新生成 ${item.customerName} / ${item.softwareName} 的更新密钥吗？\n\n原密钥会立即失效。`,
      )
    ) {
      return;
    }

    try {
      setSavingBindingId(
        item.customerSoftwareId,
      );

      const response =
        await apiFetch(
          `/api/client-update-admin/bindings/${item.customerSoftwareId}/token`,
          {
            method: "POST",
          },
        );

      if (!response.ok) {
        const text =
          await response.text();

        throw new Error(
          text ||
            `生成更新密钥失败：${response.status}`,
        );
      }

      const result =
        (await response.json()) as
          GeneratedTokenResult;

      setGeneratedToken(
        result,
      );

      await loadData();
    } catch (error) {
      console.error(
        "生成更新密钥失败：",
        error,
      );

      alert(
        error instanceof Error
          ? error.message
          : "生成更新密钥失败",
      );
    } finally {
      setSavingBindingId(
        null,
      );
    }
  }


  async function setCredentialEnabled(
    item: UpdateBindingItem,
    enabled: boolean,
  ) {
    const action =
      enabled
        ? "enable"
        : "revoke";

    try {
      setSavingBindingId(
        item.customerSoftwareId,
      );

      const response =
        await apiFetch(
          `/api/client-update-admin/bindings/${item.customerSoftwareId}/${action}`,
          {
            method: "POST",
          },
        );

      if (!response.ok) {
        const text =
          await response.text();

        throw new Error(
          text ||
            `修改更新凭证状态失败：${response.status}`,
        );
      }

      await loadData();
    } catch (error) {
      console.error(
        "修改更新凭证状态失败：",
        error,
      );

      alert(
        error instanceof Error
          ? error.message
          : "修改更新凭证状态失败",
      );
    } finally {
      setSavingBindingId(
        null,
      );
    }
  }


  async function copyToken() {
    if (!generatedToken) {
      return;
    }

    try {
      await navigator.clipboard.writeText(
        generatedToken.updateToken,
      );

      alert(
        "更新密钥已复制",
      );
    } catch {
      alert(
        "浏览器未允许自动复制，请手工选中复制",
      );
    }
  }


  /*
   * ==========================================
   * 带上传进度的 ZIP 上传
   * ==========================================
   *
   * 这里不用 apiFetch，原因和现有版本安装包上传一致：
   * fetch 没有稳定的 upload progress 事件，
   * 所以使用 XMLHttpRequest。
   *
   * JWT 仍然从 sessionStorage 读取并放入 Authorization。
   */
  function uploadUpdateZipWithProgress(
    versionId: number,
    file: File,
  ): Promise<void> {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();

      const formData = new FormData();
      formData.append("file", file);

      xhr.open(
        "POST",
        `/api/software-version-update-packages/${versionId}`,
      );

      const token = sessionStorage.getItem(
        "access_token",
      );

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

        const percent = Math.round(
          (event.loaded / event.total) * 100,
        );

        setUploadProgress(percent);

        if (percent >= 100) {
          setServerProcessing(true);
        }
      };

      xhr.onload = () => {
        setServerProcessing(false);

        if (
          xhr.status >= 200
          && xhr.status < 300
        ) {
          resolve();
          return;
        }

        reject(
          new Error(
            xhr.responseText
            || `上传更新包失败：${xhr.status}`,
          ),
        );
      };

      xhr.onerror = () => {
        setServerProcessing(false);
        reject(new Error("网络错误，更新 ZIP 上传失败"));
      };

      xhr.onabort = () => {
        setServerProcessing(false);
        reject(new Error("更新 ZIP 上传已取消"));
      };

      xhr.send(formData);
    });
  }


  async function uploadUpdatePackage() {
    if (!selectedPackageVersion) {
      return;
    }

    if (!selectedZip) {
      alert(
        "请选择 ZIP 更新包",
      );

      return;
    }

    try {
      setUploadingPackage(
        true,
      );

      setUploadProgress(0);
      setServerProcessing(false);

      await uploadUpdateZipWithProgress(
        selectedPackageVersion.versionId,
        selectedZip,
      );

      setUploadProgress(100);

      alert(
        "增量更新包生成成功",
      );

      setSelectedPackageVersion(
        null,
      );

      setSelectedZip(
        null,
      );

      await loadData();
    } catch (error) {
      console.error(
        "上传自动更新包失败：",
        error,
      );

      alert(
        error instanceof Error
          ? error.message
          : "上传自动更新包失败",
      );
    } finally {
      setUploadingPackage(
        false,
      );

      setServerProcessing(false);
    }
  }


  async function deleteUpdatePackage(
    item: UpdatePackageItem,
  ) {
    if (
      !window.confirm(
        `确定删除 ${item.softwareName} ${item.version} 的自动更新包吗？`,
      )
    ) {
      return;
    }

    try {
      const response =
        await apiFetch(
          `/api/software-version-update-packages/${item.versionId}`,
          {
            method: "DELETE",
          },
        );

      if (!response.ok) {
        const text =
          await response.text();

        throw new Error(
          text ||
            `删除更新包失败：${response.status}`,
        );
      }

      await loadData();
    } catch (error) {
      console.error(
        "删除自动更新包失败：",
        error,
      );

      alert(
        error instanceof Error
          ? error.message
          : "删除自动更新包失败",
      );
    }
  }


  async function openManifest(
    item: UpdatePackageItem,
  ) {
    try {
      setManifestLoading(
        true,
      );

      setManifest(
        null,
      );

      const response =
        await apiFetch(
          `/api/software-version-update-packages/${item.versionId}`,
        );

      if (!response.ok) {
        const text =
          await response.text();

        throw new Error(
          text ||
            `读取文件清单失败：${response.status}`,
        );
      }

      setManifest(
        (await response.json()) as
          UpdateManifest,
      );
    } catch (error) {
      console.error(
        "读取更新清单失败：",
        error,
      );

      alert(
        error instanceof Error
          ? error.message
          : "读取更新清单失败",
      );
    } finally {
      setManifestLoading(
        false,
      );
    }
  }
async function openPublishDialog(
  item: UpdatePackageItem,
) {
  if (item.versionType === "Dev") {
    alert(
      "Dev 版本仅供内部使用，不能发布给客户",
    );

    return;
  }

  try {
    const response =
      await apiFetch(
        `/api/softwareversions/${item.versionId}/publish-customers`,
      );

    if (!response.ok) {
      const text =
        await response.text();

      throw new Error(
        text ||
          `获取发布客户失败：${response.status}`,
      );
    }

    const customers =
      (await response.json()) as
        PublishCustomer[];

    setPublishCustomers(
      customers,
    );

    setPublishingVersion(
      item,
    );

    /*
     * Beta 只能指定客户。
     * Release 默认全部授权客户。
     */
    if (item.versionType === "Beta") {
      setPublishToAll(
        false,
      );
    } else {
      setPublishToAll(
        true,
      );
    }

    setSelectedCustomerIds(
      [],
    );
  } catch (error) {
    console.error(
      "获取发布客户失败：",
      error,
    );

    alert(
      error instanceof Error
        ? error.message
        : "获取发布客户失败",
    );
  }
}


function togglePublishCustomer(
  customerId: number,
) {
  setSelectedCustomerIds(
    (current) =>
      current.includes(customerId)
        ? current.filter(
            (id) =>
              id !== customerId,
          )
        : [
            ...current,
            customerId,
          ],
  );
}


async function confirmPublishVersion() {
  if (!publishingVersion) {
    return;
  }

  if (
    !publishToAll
    &&
    selectedCustomerIds.length === 0
  ) {
    alert(
      "请选择至少一个发布客户",
    );

    return;
  }

  try {
    setIsPublishing(
      true,
    );

    const response =
      await apiFetch(
        `/api/softwareversions/${publishingVersion.versionId}/publish`,
        {
          method: "POST",

          headers: {
            "Content-Type":
              "application/json",
          },

          body: JSON.stringify({
            publishToAll:
              publishToAll,

            customerIds:
              selectedCustomerIds,
          }),
        },
      );

    if (!response.ok) {
      const text =
        await response.text();

      throw new Error(
        text ||
          `发布失败：${response.status}`,
      );
    }

    const customerCount =
      publishToAll
        ? publishCustomers.length
        : selectedCustomerIds.length;

    alert(
      `${publishingVersion.softwareName} ${publishingVersion.version} 发布成功。\n\n`
      + `已发布给 ${customerCount} 家客户，客户端现在可以检测到该版本。`,
    );

    setPublishingVersion(
      null,
    );

    setPublishCustomers(
      [],
    );

    setSelectedCustomerIds(
      [],
    );

    await loadData();
  } catch (error) {
    console.error(
      "发布版本失败：",
      error,
    );

    alert(
      error instanceof Error
        ? error.message
        : "发布版本失败",
    );
  } finally {
    setIsPublishing(
      false,
    );
  }
}

  function formatFileSize(
    value: number,
  ) {
    if (value <= 0) {
      return "0 B";
    }

    if (
      value
      < 1024 * 1024
    ) {
      return `${(
        value / 1024
      ).toFixed(1)} KB`;
    }

    if (
      value
      < 1024 * 1024 * 1024
    ) {
      return `${(
        value /
        1024 /
        1024
      ).toFixed(1)} MB`;
    }

    return `${(
      value /
      1024 /
      1024 /
      1024
    ).toFixed(2)} GB`;
  }


  function formatDate(
    value: string | null,
  ) {
    if (!value) {
      return "-";
    }

    return new Date(
      value,
    ).toLocaleString(
      "zh-CN",
    );
  }


  return (
    <div className="content client-update-page">
      <div className="client-update-header">
        <div>
          <h2>
            <RefreshCw size={24} />
            客户端自动更新
          </h2>

          <p>
            管理客户更新密钥、目标版本 ZIP、文件 SHA256 清单和增量更新能力
          </p>
        </div>

        <button
          type="button"
          className="normal-button"
          disabled={loading}
          onClick={() =>
            void loadData()
          }
        >
          <RefreshCw size={15} />
          刷新
        </button>
      </div>


      <div className="client-update-intro">
        <ShieldCheck size={21} />

        <div>
          <strong>
            文件级增量更新
          </strong>

          <span>
            客户端比较本地文件与目标版本 SHA256，只下载变化文件；完整安装包继续作为失败兜底。
          </span>
        </div>
      </div>


      {errorMessage && (
        <div className="download-record-error">
          {errorMessage}
        </div>
      )}


      {isAdmin && (
        <section className="client-update-section">
          <div className="client-update-section-title">
            <div>
              <h3>
                <KeyRound size={18} />
                客户更新密钥
              </h3>

              <p>
                每个“客户 + 软件”一套独立 Token。数据库只保存 SHA256，明文只显示一次。
              </p>
            </div>

            <input
              className="client-update-search"
              value={bindingKeyword}
              placeholder="搜索客户或软件"
              onChange={(event) =>
                setBindingKeyword(
                  event.target.value,
                )
              }
            />
          </div>


          <div className="client-update-table-wrap">
            <table className="client-update-table">
              <thead>
                <tr>
                  <th>客户</th>
                  <th>软件</th>
                  <th>授权</th>
                  <th>更新密钥</th>
                  <th>最后使用</th>
                  <th>操作</th>
                </tr>
              </thead>

              <tbody>
                {filteredBindings.map(
                  (item) => {
                    const available =
                      item.bindingEnabled
                      &&
                      item.customerEnabled
                      &&
                      item.softwareEnabled;

                    return (
                      <tr
                        key={
                          item.customerSoftwareId
                        }
                      >
                        <td>
                          <strong>
                            {item.customerName}
                          </strong>

                          <small>
                            {item.customerCode}
                          </small>
                        </td>

                        <td>
                          <strong>
                            {item.softwareName}
                          </strong>

                          <small>
                            {item.softwareCode}
                          </small>
                        </td>

                        <td>
                          <span
                            className={
                              available
                                ? "client-update-badge is-ready"
                                : "client-update-badge is-off"
                            }
                          >
                            {available
                              ? "有效"
                              : "停用"}
                          </span>
                        </td>

                        <td>
                          {!item.credentialExists ? (
                            <span className="client-update-badge is-warn">
                              未生成
                            </span>
                          ) : (
                            <>
                              <span
                                className={
                                  item.credentialEnabled
                                    ? "client-update-badge is-ready"
                                    : "client-update-badge is-off"
                                }
                              >
                                {item.credentialEnabled
                                  ? "已启用"
                                  : "已停用"}
                              </span>

                              <small>
                                {item.tokenPrefix || "-"}
                              </small>
                            </>
                          )}
                        </td>

                        <td>
                          {formatDate(
                            item.lastUsedAt,
                          )}
                        </td>

                        <td>
                          <div className="table-actions">
                            <button
                              type="button"
                              className="normal-button"
                              disabled={
                                !available
                                ||
                                savingBindingId
                                  ===
                                  item.customerSoftwareId
                              }
                              onClick={() =>
                                void generateToken(
                                  item,
                                )
                              }
                            >
                              {item.credentialExists
                                ? "重置密钥"
                                : "生成密钥"}
                            </button>

                            {item.credentialExists
                              &&
                              item.credentialEnabled && (
                                <button
                                  type="button"
                                  className="normal-button"
                                  disabled={
                                    savingBindingId
                                      ===
                                      item.customerSoftwareId
                                  }
                                  onClick={() =>
                                    void setCredentialEnabled(
                                      item,
                                      false,
                                    )
                                  }
                                >
                                  停用
                                </button>
                              )}

                            {item.credentialExists
                              &&
                              !item.credentialEnabled && (
                                <button
                                  type="button"
                                  className="normal-button"
                                  disabled={
                                    !available
                                    ||
                                    savingBindingId
                                      ===
                                      item.customerSoftwareId
                                  }
                                  onClick={() =>
                                    void setCredentialEnabled(
                                      item,
                                      true,
                                    )
                                  }
                                >
                                  启用
                                </button>
                              )}
                          </div>
                        </td>
                      </tr>
                    );
                  },
                )}

                {filteredBindings.length === 0 && (
                  <tr>
                    <td
                      colSpan={6}
                      className="client-update-empty"
                    >
                      暂无符合条件的客户软件绑定
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      )}


      <section className="client-update-section">
        <div className="client-update-section-title">
          <div>
            <h3>
              <FileArchive size={18} />
              自动更新包
            </h3>

            <p>
              草稿版本上传目标版本完整目录 ZIP。发布后更新包锁定，不允许再修改。
            </p>
          </div>

          <input
            className="client-update-search"
            value={packageKeyword}
            placeholder="搜索软件或版本"
            onChange={(event) =>
              setPackageKeyword(
                event.target.value,
              )
            }
          />
        </div>


        <div className="client-update-package-tip">
          ZIP 内可放
          <code>.update-ignore.txt</code>
          排除客户配置/日志；
          <code>.update-delete.txt</code>
          声明升级后需要删除的旧文件。
          <code>updater/</code>
          和
          <code>version.txt</code>
          默认不会被更新包覆盖。
        </div>


        <div className="client-update-table-wrap">
          <table className="client-update-table">
            <thead>
              <tr>
                <th>软件</th>
                <th>版本</th>
                <th>发布状态</th>
                <th>增量包</th>
                <th>完整安装包</th>
                <th>操作</th>
              </tr>
            </thead>

            <tbody>
              {filteredPackages.map(
                (item) => (
                  <tr
                    key={
                      item.versionId
                    }
                  >
                    <td>
                      <strong>
                        {item.softwareName}
                      </strong>

                      <small>
                        {item.softwareCode}
                      </small>
                    </td>

                 <td>
  <strong>
    {item.version}
  </strong>

  <small>
    {item.versionType}
  </small>
</td>

                    <td>
                      {item.publishStatus}
                    </td>

                    <td>
                      {item.hasUpdatePackage ? (
                        <>
                          <span className="client-update-badge is-ready">
                            已生成
                          </span>

                          <small>
                            {item.fileCount} 个文件 ·{" "}
                            {formatFileSize(
                              item.totalFileSize,
                            )}
                            {item.deleteCount > 0
                              ? ` · 删除 ${item.deleteCount}`
                              : ""}
                          </small>
                        </>
                      ) : (
                        <span className="client-update-badge is-warn">
                          未上传
                        </span>
                      )}
                    </td>

                    <td>
                      {item.fullPackageAvailable ? (
                        <>
                          <span className="client-update-badge is-ready">
                            已有
                          </span>

                          <small>
                            {item.fullPackageFileName || "-"}
                          </small>
                        </>
                      ) : (
                        <span className="client-update-badge is-off">
                          无
                        </span>
                      )}
                    </td>

                    <td>
                      <div className="table-actions">
                        {item.publishStatus === "Draft" && (
                          <button
                            type="button"
                            className="normal-button"
                            onClick={() => {
                              setSelectedPackageVersion(
                                item,
                              );

                              setSelectedZip(
                                null,
                              );

                              setUploadProgress(0);
                              setServerProcessing(false);
                            }}
                          >
                            <UploadCloud size={14} />
                            {item.hasUpdatePackage
                              ? "更换 ZIP"
                              : "上传 ZIP"}
                          </button>
                        )}

                        {item.hasUpdatePackage && (
                          <button
                            type="button"
                            className="normal-button"
                            disabled={
                              manifestLoading
                            }
                            onClick={() =>
                              void openManifest(
                                item,
                              )
                            }
                          >
                            查看清单
                          </button>
                        )}
                        {item.publishStatus === "Draft"
  &&
  item.hasUpdatePackage
  &&
  item.versionType !== "Dev" && (
    <button
      type="button"
      className="primary-button"
      onClick={() =>
        void openPublishDialog(
          item,
        )
      }
    >
      发布
    </button>
  )}

                        {item.publishStatus === "Draft"
                          &&
                          item.hasUpdatePackage && (
                            <button
                              type="button"
                              className="delete-button"
                              onClick={() =>
                                void deleteUpdatePackage(
                                  item,
                                )
                              }
                            >
                              删除
                            </button>
                          )}
                      </div>
                    </td>
                  </tr>
                ),
              )}

              {filteredPackages.length === 0 && (
                <tr>
                  <td
                    colSpan={6}
                    className="client-update-empty"
                  >
                    暂无符合条件的软件版本
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>


      {generatedToken && (
        <div className="version-attachment-mask">
          <div className="client-update-dialog">
            <div className="client-update-dialog-title">
              <CheckCircle2 size={22} />

              <div>
                <h3>
                  更新密钥已生成
                </h3>

                <p>
                  {generatedToken.customerName}
                  {" · "}
                  {generatedToken.softwareName}
                </p>
              </div>
            </div>

            <div className="client-update-secret-warning">
              完整 Token 只显示这一次。关闭后服务器无法恢复明文，如遗失只能重新生成。
            </div>

            <label>
              SoftwareCode
            </label>

            <input
              readOnly
              value={
                generatedToken.softwareCode
              }
            />

            <label>
              UpdateToken
            </label>

            <textarea
              readOnly
              value={
                generatedToken.updateToken
              }
            />

            <div className="form-buttons">
              <button
                type="button"
                className="primary-button"
                onClick={() =>
                  void copyToken()
                }
              >
                <Copy size={15} />
                复制 Token
              </button>

              <button
                type="button"
                className="normal-button"
                onClick={() =>
                  setGeneratedToken(
                    null,
                  )
                }
              >
                已保存，关闭
              </button>
            </div>
          </div>
        </div>
      )}


      {selectedPackageVersion && (
        <div className="version-attachment-mask">
          <div className="client-update-dialog">
            <div className="client-update-dialog-title">
              <UploadCloud size={22} />

              <div>
                <h3>
                  上传目标版本目录 ZIP
                </h3>

                <p>
                  {selectedPackageVersion.softwareName}
                  {" · "}
                  {selectedPackageVersion.version}
                </p>
              </div>
            </div>

            <div className="client-update-secret-warning">
              ZIP 应包含这个版本实际运行目录的完整文件状态。服务端会自动计算 SHA256，客户端只下载变化文件。
            </div>

            <input
              type="file"
              accept=".zip,application/zip"
              disabled={uploadingPackage}
              onChange={(event) => {
                setSelectedZip(
                  event.target.files?.[0]
                  ?? null,
                );

                setUploadProgress(0);
                setServerProcessing(false);
              }}
            />

            {uploadingPackage && (
              <div className="client-update-upload-progress">
                <div className="client-update-progress-track">
                  <div
                    className="client-update-progress-value"
                    style={{
                      width: `${uploadProgress}%`,
                    }}
                  />
                </div>

                <div className="client-update-progress-text">
                  {uploadProgress < 100
                    ? `正在上传：${uploadProgress}%`
                    : serverProcessing
                      ? "上传完成，服务器正在解压并计算 SHA256..."
                      : "正在完成更新包生成..."}
                </div>
              </div>
            )}

            <div className="form-buttons">
              <button
                type="button"
                className="primary-button"
                disabled={
                  !selectedZip
                  ||
                  uploadingPackage
                }
                onClick={() =>
                  void uploadUpdatePackage()
                }
              >
                {uploadingPackage
                  ? uploadProgress < 100
                    ? `上传中 ${uploadProgress}%`
                    : "处理中..."
                  : "上传并生成更新包"}
              </button>

              <button
                type="button"
                className="normal-button"
                disabled={
                  uploadingPackage
                }
                onClick={() => {
                  setSelectedPackageVersion(
                    null,
                  );

                  setSelectedZip(
                    null,
                  );

                  setUploadProgress(0);
                  setServerProcessing(false);
                }}
              >
                取消
              </button>
            </div>
          </div>
        </div>
      )}


      {(manifest || manifestLoading) && (
        <div className="version-attachment-mask">
          <div className="client-update-dialog client-update-manifest-dialog">
            <div className="client-update-dialog-title">
              <FileArchive size={22} />

              <div>
                <h3>
                  目标版本文件清单
                </h3>

                {manifest && (
                  <p>
                    {manifest.version}
                    {" · "}
                    {manifest.files.length} 个文件
                    {" · "}
                    {formatFileSize(
                      manifest.totalFileSize,
                    )}
                  </p>
                )}
              </div>
            </div>

            {manifestLoading ? (
              <div className="client-update-empty">
                正在读取清单...
              </div>
            ) : manifest ? (
              <>
                {manifest.deletePaths.length > 0 && (
                  <div className="client-update-delete-list">
                    <strong>
                      升级时删除：
                    </strong>

                    {manifest.deletePaths.map(
                      (path) => (
                        <code key={path}>
                          {path}
                        </code>
                      ),
                    )}
                  </div>
                )}

                <div className="client-update-manifest-list">
                  {manifest.files.map(
                    (file) => (
                      <div
                        key={file.path}
                        className="client-update-manifest-item"
                      >
                        <div>
                          <strong>
                            {file.path}
                          </strong>

                          <small>
                            {formatFileSize(
                              file.size,
                            )}
                          </small>
                        </div>

                        <code>
                          {file.sha256}
                        </code>
                      </div>
                    ),
                  )}
                </div>
              </>
            ) : null}

            <div className="form-buttons">
              <button
                type="button"
                className="normal-button"
                onClick={() => {
                  setManifest(
                    null,
                  );

                  setManifestLoading(
                    false,
                  );
                }}
              >
                关闭
              </button>
            </div>
          </div>
        </div>
      )}
{publishingVersion && (
  <div className="version-attachment-mask">
    <div className="client-update-dialog">
      <div className="client-update-dialog-title">
        <CheckCircle2 size={22} />

        <div>
          <h3>
            发布自动更新版本
          </h3>

          <p>
            {publishingVersion.softwareName}
            {" · "}
            {publishingVersion.version}
            {" · "}
            {publishingVersion.versionType}
          </p>
        </div>
      </div>

      <div className="client-update-secret-warning">
        发布后该版本的自动更新 ZIP 将锁定。
        客户端只有在版本已发布并且属于发布范围时，
        才能检测到该版本。
      </div>

      <div className="form-section">
        <h4>
          发布范围
        </h4>

        <label>
          <input
            type="radio"
            checked={
              publishToAll
            }
            disabled={
              publishingVersion.versionType
                === "Beta"
            }
            onChange={() =>
              setPublishToAll(
                true,
              )
            }
          />

          {" "}
          全部授权客户
          {" "}
          （{publishCustomers.length} 家）
        </label>

        <br />

        <label>
          <input
            type="radio"
            checked={
              !publishToAll
            }
            onChange={() =>
              setPublishToAll(
                false,
              )
            }
          />

          {" "}
          指定客户
        </label>
      </div>

      {!publishToAll && (
        <div className="form-section">
          <h4>
            选择客户
          </h4>

          {publishCustomers.length === 0 ? (
            <div className="client-update-empty">
              当前没有已授权客户
            </div>
          ) : (
            publishCustomers.map(
              (customer) => (
                <label
                  key={
                    customer.customerId
                  }
                  style={{
                    display: "block",
                    marginBottom: 10,
                  }}
                >
                  <input
                    type="checkbox"
                    checked={
                      selectedCustomerIds
                        .includes(
                          customer.customerId,
                        )
                    }
                    onChange={() =>
                      togglePublishCustomer(
                        customer.customerId,
                      )
                    }
                  />

                  {" "}
                  {customer.name}
                  {" · "}
                  {customer.code}

                  {(customer.province
                    ||
                    customer.city) && (
                    <>
                      {" · "}
                      {customer.province}
                      {customer.city}
                    </>
                  )}
                </label>
              ),
            )
          )}
        </div>
      )}

      {publishingVersion.versionType
        === "Beta" && (
        <div className="client-update-package-tip">
          Beta 版本只能发布给指定客户。
        </div>
      )}

      <div className="form-buttons">
        <button
          type="button"
          className="primary-button"
          disabled={
            isPublishing
            ||
            (
              !publishToAll
              &&
              selectedCustomerIds.length
                === 0
            )
          }
          onClick={() =>
            void confirmPublishVersion()
          }
        >
          {isPublishing
            ? "发布中..."
            : "确认发布"}
        </button>

        <button
          type="button"
          className="normal-button"
          disabled={
            isPublishing
          }
          onClick={() => {
            setPublishingVersion(
              null,
            );

            setPublishCustomers(
              [],
            );

            setSelectedCustomerIds(
              [],
            );
          }}
        >
          取消
        </button>
      </div>
    </div>
  </div>
)}

      {loading && (
        <div className="dashboard-empty">
          正在加载自动更新配置...
        </div>
      )}
    </div>
  );
}


export default ClientUpdatePage;
