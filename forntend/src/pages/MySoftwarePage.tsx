import { useEffect, useState } from "react";

import { apiFetch } from "../services/api";

/*
 * 最新版本信息。
 *
 * 如果某个软件还没有发布正式 Release 版本，
 * latestVersion 会是 null。
 */
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

/*
 * 客户可以看到的版本附件。
 *
 * 后端已经过滤：
 * IsCustomerVisible = true
 *
 * 所以这里不会收到内部问题日志。
 */
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

/*
 * 当前客户拥有的软件。
 *
 * 对应后端：
 *
 * GET /api/my-software
 */
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

function MySoftwarePage() {
  /*
   * 当前客户拥有的软件列表。
   */
  const [softwareList, setSoftwareList] = useState<MySoftware[]>([]);
  /*
   * 各个版本对应的客户可见资料。
   *
   * 例如：
   *
   * {
   *   5: [附件1, 附件2],
   *   8: [附件3]
   * }
   *
   * 这里的 5、8 都是 SoftwareVersion.Id。
   */
  const [versionAttachments, setVersionAttachments] = useState<
    Record<number, MyVersionAttachment[]>
  >({});
  /*
   * 是否正在加载。
   */
  const [loading, setLoading] = useState(true);

  /*
   * 错误信息。
   */
  const [errorMessage, setErrorMessage] = useState("");
  /*
   * ======================================
   * 加载某个版本的客户可见资料
   * ======================================
   */
  async function loadVersionAttachments(versionId: number) {
    try {
      const response = await apiFetch(
        `/api/my-software/versions/${versionId}/attachments`,
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取版本资料失败：${response.status}`);
      }

      const data = (await response.json()) as MyVersionAttachment[];

      /*
       * 只更新当前 versionId 对应的数据。
       */
      setVersionAttachments((current) => ({
        ...current,

        [versionId]: data,
      }));
    } catch (error) {
      console.error(`加载版本 ${versionId} 的资料失败：`, error);

      /*
       * 某个版本资料加载失败，
       * 不应该导致整个“我的软件”页面打不开。
       */
      setVersionAttachments((current) => ({
        ...current,

        [versionId]: [],
      }));
    }
  }
  /*
   * ======================================
   * 加载当前客户的软件
   * ======================================
   */
  async function loadMySoftware() {
    try {
      setLoading(true);

      setErrorMessage("");

      /*
       * 注意：
       *
       * 这里没有：
       *
       * ?customerId=1
       *
       * 因为当前客户是谁，
       * 由后端根据 JWT 判断。
       */
      const response = await apiFetch("/api/my-software");

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取我的软件失败：${response.status}`);
      }

      /*
       * 后端返回 JSON 数组。
       */
      const data = (await response.json()) as MySoftware[];

      setSoftwareList(data);

      /*
       * 每次重新加载软件时，
       * 先清空旧的附件缓存。
       */
      setVersionAttachments({});

      /*
       * 给每一个存在“最新正式版本”的软件，
       * 加载它的客户可见资料。
       */
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

  /*
   * 页面第一次打开时，
   * 自动加载软件。
   */
  useEffect(() => {
    loadMySoftware();
  }, []);
  /*
   * ======================================
   * 版本资料类型中文名称
   * ======================================
   */
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
  /*
   * ======================================
   * 格式化文件大小
   * ======================================
   *
   * 数据库存的是字节：
   *
   * 1048576
   *
   * 转成人容易阅读的：
   *
   * 1 MB
   */
  function formatFileSize(bytes: number) {
    if (bytes <= 0) {
      return "-";
    }

    const kb = bytes / 1024;

    if (kb < 1024) {
      return `${kb.toFixed(1)} KB`;
    }

    const mb = kb / 1024;

    if (mb < 1024) {
      return `${mb.toFixed(1)} MB`;
    }

    const gb = mb / 1024;

    return `${gb.toFixed(2)} GB`;
  }

  /*
   * ======================================
   * 格式化日期
   * ======================================
   */
  function formatDate(dateText: string | null) {
    if (!dateText) {
      return "-";
    }

    const date = new Date(dateText);

    return date.toLocaleString();
  }
  /*
   * ======================================
   * 下载软件安装包
   * ======================================
   */
  async function downloadPackage(versionId: number) {
    try {
      /*
       * 第一步：
       *
       * 携带 JWT 请求短时下载票据。
       */
      const response = await apiFetch(
        `/api/download/version/${versionId}/ticket`,
        {
          method: "POST",
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取下载地址失败：${response.status}`);
      }

      /*
       * 后端返回：
       *
       * {
       *   ticket: "...",
       *   expiresInSeconds: 300,
       *   downloadUrl: "/api/download/file?ticket=..."
       * }
       */
      const data = await response.json();

      /*
       * 创建一个临时 <a>。
       *
       * 浏览器自己执行文件下载，
       * React 不读取安装包内容。
       */
      const link = document.createElement("a");

      link.href = data.downloadUrl;

      /*
       * 不需要手工指定文件名。
       *
       * 后端 File()
       * 会通过 Content-Disposition
       * 告诉浏览器真正的文件名。
       */
      document.body.appendChild(link);

      link.click();

      link.remove();
    } catch (error) {
      console.error("下载安装包失败：", error);

      alert("下载安装包失败：" + String(error));
    }
  }

  /*
   * ======================================
   * 下载版本附加资料
   * ======================================
   */
  async function downloadVersionAttachment(attachment: MyVersionAttachment) {
    try {
      /*
       * 这个接口需要 Customer JWT，
       * 所以必须使用 apiFetch。
       */
      const response = await apiFetch(
        `/api/my-software/attachments/${attachment.id}/download`,
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `下载资料失败：${response.status}`);
      }

      /*
       * 把服务器返回的文件读取成 Blob。
       */
      const blob = await response.blob();

      /*
       * 创建浏览器临时下载地址。
       */
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
  return (
    <div className="content">
      {/* 页面标题 */}
      <div className="title-row">
        <h2>我的软件</h2>
      </div>

      {/* ================================
          加载状态
          ================================ */}
      {loading && (
        <div className="my-software-message">正在加载您的软件...</div>
      )}

      {/* ================================
          错误状态
          ================================ */}
      {!loading && errorMessage && (
        <div className="my-software-error">
          {errorMessage}

          <button className="normal-button" onClick={loadMySoftware}>
            重新加载
          </button>
        </div>
      )}

      {/* ================================
          没有软件
          ================================ */}
      {!loading && !errorMessage && softwareList.length === 0 && (
        <div className="my-software-empty">
          <div className="my-software-empty-title">暂无授权软件</div>

          <div className="my-software-empty-text">
            当前账号所属客户暂未绑定任何软件， 请联系软件服务人员进行授权。
          </div>
        </div>
      )}

      {/* ================================
          软件卡片列表
          ================================ */}
      {!loading && !errorMessage && softwareList.length > 0 && (
        <div className="my-software-grid">
          {softwareList.map((software) => (
            <div className="my-software-card" key={software.softwareId}>
              {/* 软件卡片顶部 */}
              <div className="my-software-card-header">
                <div>
                  <div className="my-software-name">
                    {software.softwareName}
                  </div>

                  <div className="my-software-code">
                    {software.softwareCode}
                  </div>
                </div>

                {software.category && (
                  <span className="my-software-category">
                    {software.category}
                  </span>
                )}
              </div>

              {/* 软件描述 */}
              <div className="my-software-description">
                {software.description || "暂无软件说明"}
              </div>

              {/* 软件基本信息 */}
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

              {/* ================================
                      最新版本
                      ================================ */}
              <div className="my-version-area">
                {software.latestVersion === null ? (
                  <div className="my-version-empty">暂无已发布的正式版本</div>
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
                        发布时间：
                        {formatDate(software.latestVersion.publishedAt)}
                      </span>

                      {software.latestVersion.packageFileName && (
                        <span>
                          安装包：
                          {software.latestVersion.packageFileName}
                        </span>
                      )}

                      {software.latestVersion.packageFileSize > 0 && (
                        <span>
                          大小：
                          {formatFileSize(
                            software.latestVersion.packageFileSize,
                          )}
                        </span>
                      )}
                    </div>

                    {/* 下载按钮 */}
                    <div className="my-version-actions">
                      <button
                        type="button"
                        className="primary-button"
                        disabled={!software.latestVersion.canDownload}
                        onClick={() =>
                          downloadPackage(software.latestVersion!.id)
                        }
                      >
                        {software.latestVersion.canDownload
                          ? "下载安装包"
                          : "暂不可下载"}
                      </button>
                    </div>

                    {/* ================================
    版本资料
    ================================ */}
                    <div className="my-version-resources">
                      <div className="my-version-resources-title">版本资料</div>

                      {(versionAttachments[software.latestVersion.id] ?? [])
                        .length === 0 ? (
                        <div className="my-version-resources-empty">
                          暂无可下载资料
                        </div>
                      ) : (
                        <div className="my-version-resources-list">
                          {(
                            versionAttachments[software.latestVersion.id] ?? []
                          ).map((attachment) => (
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

                                  <span>
                                    {formatFileSize(attachment.fileSize)}
                                  </span>

                                  {attachment.remark && (
                                    <span>{attachment.remark}</span>
                                  )}
                                </div>
                              </div>

                              <button
                                type="button"
                                className="normal-button"
                                onClick={() =>
                                  downloadVersionAttachment(attachment)
                                }
                              >
                                下载
                              </button>
                            </div>
                          ))}
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
    </div>
  );
}

export default MySoftwarePage;
