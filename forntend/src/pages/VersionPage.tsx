import { useEffect, useState } from "react";

import PageHeader from "../components/PageHeader";
import SearchBar from "../components/SearchBar";
import ConfirmDeleteButton from "../components/ConfirmDeleteButton";
import { apiFetch } from "../services/api";
/**
 * 软件信息。
 *
 * 版本页面暂时只需要：
 * id
 * name
 * code
 */
interface Software {
  id: number;
  name: string;
  code: string;

  // 软件是否启用
  isEnabled: boolean;

  // 软件是否允许下载
  allowDownload: boolean;
}
interface PublishedCustomer {
  customerId: number;

  name: string;

  code: string;

  province: string;

  city: string;

  publishedToCustomerAt: string;
}

interface PublishedCustomerResult {
  versionId: number;

  version: string;

  versionType: string;

  publishStatus: string;

  customerCount: number;

  customers: PublishedCustomer[];
}
/**
 * 软件版本信息。
 *
 * 字段需要与后端 SoftwareVersion 返回的 JSON 对应。
 */
interface SoftwareVersion {
  id: number;

  // 所属软件ID
  softwareId: number;

  // 版本号，例如：1.0.0
  version: string;

  // Release / Beta / Dev
  versionType: string;

  // 版本标题
  title: string;

  // 更新说明
  releaseNotes: string;

  // 是否已发布
  isPublished: boolean;

  // 是否强制升级
  forceUpdate: boolean;

  // 是否允许下载
  allowDownload: boolean;

  // 发布时间
  publishedAt: string | null;

  createdAt: string;
  updatedAt: string;

  // 安装包原始文件名
  packageFileName: string;

  // 安装包大小，单位：字节
  packageFileSize: number;

  // 安装包 SHA256
  packageSha256: string;

  // 安装包上传时间
  packageUploadedAt: string | null;

  // Draft / Published / Deprecated
  publishStatus: string;
}

interface PublishCustomer {
  customerId: number;
  name: string;
  code: string;
  province: string;
  city: string;
}
/**
 * 软件版本附加资料。
 *
 * 与后端 SoftwareVersionAttachment
 * 查询接口返回的数据对应。
 */
interface VersionAttachment {
  id: number;

  softwareVersionId: number;

  fileName: string;

  fileSize: number;

  contentType: string;

  attachmentType: string;

  isCustomerVisible: boolean;

  remark: string;

  uploadedByUserId: number;

  uploadedByName: string;

  createdAt: string;
}
function VersionPage() {
  /**
   * 软件列表。
   *
   * 为什么版本页面还需要查询软件？
   *
   * 因为 SoftwareVersion 里面只有：
   *
   * softwareId = 1
   *
   * 但是页面上我们希望显示：
   *
   * 路面检测数据处理软件
   *
   * 所以需要同时获取软件列表。
   */
  const [softwares, setSoftwares] = useState<Software[]>([]);

  /**
   * 软件版本列表
   */
  const [versions, setVersions] = useState<SoftwareVersion[]>([]);

  /**
   * 搜索关键字
   */
  const [searchKeyword, setSearchKeyword] = useState("");

  /**
   * 是否显示新增/编辑版本表单
   */
  const [showVersionForm, setShowVersionForm] = useState(false);

  /**
   * 当前正在编辑的版本ID
   *
   * null：
   * 说明现在是“新增”
   *
   * 有值：
   * 说明现在是“编辑”
   */
  const [editingVersionId, setEditingVersionId] = useState<number | null>(null);

  /**
   * =========================
   * 版本表单字段
   * =========================
   */

  /**
   * 所属软件ID
   *
   * 注意：
   * HTML select 的 value 默认是字符串，
   * 但我们的 SoftwareId 是 number。
   *
   * 后面会讲怎么转换。
   */
  const [softwareId, setSoftwareId] = useState<number>(0);

  /**
   * 版本号
   */
  const [version, setVersion] = useState("");

  /**
   * 版本类型
   *
   * 默认正式版
   */
  const [versionType, setVersionType] = useState("Release");

  /**
   * 版本标题
   */
  const [title, setTitle] = useState("");

  /**
   * 更新说明
   */
  const [releaseNotes, setReleaseNotes] = useState("");

  /**
   * 是否强制升级
   */
  const [forceUpdate, setForceUpdate] = useState(false);

  /**
   * 是否允许下载
   */
  const [allowDownload, setAllowDownload] = useState(true);

  /**
   * 用户当前选择的安装包。
   *
   * File 是浏览器提供的文件对象。
   *
   * null 表示目前没有选择文件。
   */
  const [selectedPackage, setSelectedPackage] = useState<File | null>(null);

  /**
   * 当前准备上传安装包的软件版本。
   *
   * null：
   * 当前没有打开上传区域
   *
   * 有值：
   * 当前正在准备给这个版本上传安装包
   */
  const [selectedUploadVersion, setSelectedUploadVersion] =
    useState<SoftwareVersion | null>(null);

  /**
   * 当前正在上传哪个版本。
   *
   * null：
   * 没有上传任务
   *
   * 有数字：
   * 正在上传这个版本ID的安装包
   */
  const [uploadingVersionId, setUploadingVersionId] = useState<number | null>(
    null,
  );
  /**
   * 当前正在管理资料的软件版本。
   *
   * null：
   * 资料窗口关闭。
   */
  const [selectedAttachmentVersion, setSelectedAttachmentVersion] =
    useState<SoftwareVersion | null>(null);

  /**
   * 当前版本已有附件。
   */
  const [versionAttachments, setVersionAttachments] = useState<
    VersionAttachment[]
  >([]);

  /**
   * 准备上传的附件文件。
   */
  const [selectedAttachmentFile, setSelectedAttachmentFile] =
    useState<File | null>(null);

  /**
   * 附件类型。
   */
  const [attachmentType, setAttachmentType] = useState("Manual");

  /**
   * 客户是否可以看到。
   */
  const [attachmentCustomerVisible, setAttachmentCustomerVisible] =
    useState(true);

  /**
   * 附件备注。
   */
  const [attachmentRemark, setAttachmentRemark] = useState("");

  /**
   * 是否正在上传版本附件。
   */
  const [isUploadingAttachment, setIsUploadingAttachment] = useState(false);

  /**
   * 用于重新生成 file input。
   *
   * 上传成功以后递增，
   * 可以把浏览器中的已选文件真正清空。
   */
  const [attachmentFileInputKey, setAttachmentFileInputKey] = useState(0);
  /**
   * 从后端加载软件列表
   */

  const [uploadProgress, setUploadProgress] = useState(0);

  const [isServerProcessing, setIsServerProcessing] = useState(false);

  const [publishingVersion, setPublishingVersion] =
    useState<SoftwareVersion | null>(null);

  const [publishCustomers, setPublishCustomers] = useState<PublishCustomer[]>(
    [],
  );

  const [publishToAll, setPublishToAll] = useState(true);

  const [selectedCustomerIds, setSelectedCustomerIds] = useState<number[]>([]);

  const [isPublishing, setIsPublishing] = useState(false);

  const [publishedCustomerData, setPublishedCustomerData] =
    useState<PublishedCustomerResult | null>(null);

  const [publishedCustomerLoading, setPublishedCustomerLoading] =
    useState(false);
  async function loadSoftwares() {
    try {
      const response = await apiFetch("/api/softwares");

      if (!response.ok) {
        throw new Error(`获取软件列表失败：${response.status}`);
      }

      const data: Software[] = await response.json();

      setSoftwares(data);
    } catch (error) {
      console.error("获取软件列表失败：", error);
    }
  }

  async function openPublishedCustomers(softwareVersion: SoftwareVersion) {
    try {
      setPublishedCustomerLoading(true);

      /*
       * 先给一个临时对象，
       * 让弹窗立即打开并显示加载状态。
       */
      setPublishedCustomerData({
        versionId: softwareVersion.id,

        version: softwareVersion.version,

        versionType: softwareVersion.versionType,

        publishStatus: softwareVersion.publishStatus,

        customerCount: 0,

        customers: [],
      });

      const response = await apiFetch(
        `/api/softwareversions/${softwareVersion.id}/published-customers`,
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取发布客户失败：${response.status}`);
      }

      const data = (await response.json()) as PublishedCustomerResult;

      setPublishedCustomerData(data);
    } catch (error) {
      console.error("获取版本发布客户失败：", error);

      alert("获取发布客户失败：" + String(error));

      setPublishedCustomerData(null);
    } finally {
      setPublishedCustomerLoading(false);
    }
  }
  function uploadPackageWithProgress(
    softwareVersionId: number,
    file: File,
  ): Promise<void> {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();

      const formData = new FormData();
      formData.append("file", file);

      xhr.open("POST", `/api/softwareversions/${softwareVersionId}/package`);

      /*
       * 因为这里没有经过 apiFetch，
       * JWT 要自己加。
       */
      const token = sessionStorage.getItem("access_token");

      if (token) {
        xhr.setRequestHeader("Authorization", `Bearer ${token}`);
      }

      /*
       * 上传进度事件
       */
      xhr.upload.onprogress = (event) => {
        if (!event.lengthComputable) {
          return;
        }

        const percent = Math.round((event.loaded / event.total) * 100);

        setUploadProgress(percent);

        /*
         * 100% 表示浏览器已经把文件发给服务器，
         * 但后端可能还在保存文件、计算 SHA256。
         */
        if (percent >= 100) {
          setIsServerProcessing(true);
        }
      };

      xhr.onload = () => {
        setIsServerProcessing(false);

        if (xhr.status >= 200 && xhr.status < 300) {
          resolve();
        } else {
          reject(new Error(xhr.responseText || `上传失败：${xhr.status}`));
        }
      };

      xhr.onerror = () => {
        setIsServerProcessing(false);

        reject(new Error("网络错误，安装包上传失败"));
      };

      xhr.onabort = () => {
        setIsServerProcessing(false);

        reject(new Error("安装包上传已取消"));
      };

      xhr.send(formData);
    });
  }

  async function openPublishDialog(softwareVersion: SoftwareVersion) {
    try {
      const response = await apiFetch(
        `/api/softwareversions/${softwareVersion.id}/publish-customers`,
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取客户列表失败：${response.status}`);
      }

      const data = (await response.json()) as PublishCustomer[];

      setPublishCustomers(data);

      setPublishingVersion(softwareVersion);

      /*
       * Beta 只能指定客户。
       */
      if (softwareVersion.versionType === "Beta") {
        setPublishToAll(false);
      } else {
        setPublishToAll(true);
      }

      setSelectedCustomerIds([]);
    } catch (error) {
      console.error("获取发布客户失败：", error);

      alert("获取发布客户失败：" + String(error));
    }
  }

  /**
   * 从后端加载版本列表
   */
  async function loadVersions() {
    try {
      const response = await apiFetch("/api/softwareversions");

      if (!response.ok) {
        throw new Error(`获取版本列表失败：${response.status}`);
      }

      const data: SoftwareVersion[] = await response.json();

      setVersions(data);
    } catch (error) {
      console.error("获取版本列表失败：", error);
    }
  }
  /**
   * 清空版本表单
   *
   * 新增完成、取消编辑时都会使用。
   */
  function clearVersionForm() {
    setSoftwareId(0);
    setVersion("");
    setVersionType("Release");
    setTitle("");
    setReleaseNotes("");
    setForceUpdate(false);
    setAllowDownload(true);

    setEditingVersionId(null);
  }
  /**
   * 页面第一次打开时加载数据。
   */
  useEffect(() => {
    loadSoftwares();
    loadVersions();
  }, []);

  /**
   * 根据 SoftwareId 找到对应的软件名称。
   *
   * 例如：
   *
   * softwareId = 2
   *
   * 从 softwares 中找到：
   *
   * {
   *   id: 2,
   *   name: "路面检测软件"
   * }
   *
   * 最终返回：
   *
   * "路面检测软件"
   */
  function getSoftwareName(softwareId: number) {
    const software = softwares.find((x) => x.id === softwareId);

    if (!software) {
      return "未知软件";
    }

    return software.name;
  }

  /**
   * 搜索过滤
   */
  const filteredVersions = versions.filter((version) => {
    const keyword = searchKeyword.trim().toLowerCase();

    // 没有搜索条件就显示全部
    if (keyword === "") {
      return true;
    }

    const softwareName = getSoftwareName(version.softwareId).toLowerCase();

    return (
      softwareName.includes(keyword) ||
      version.version.toLowerCase().includes(keyword) ||
      version.versionType.toLowerCase().includes(keyword) ||
      version.title.toLowerCase().includes(keyword)
    );
  });
  /**
   * 保存版本
   *
   * editingVersionId === null
   * → POST 新增
   *
   * editingVersionId 有值
   * → PUT 修改
   */
  async function saveVersion() {
    /**
     * 基本验证
     */
    if (softwareId <= 0) {
      alert("请选择所属软件");
      return;
    }

    if (version.trim() === "") {
      alert("请输入版本号");
      return;
    }

    /**
     * 准备发送给后端的数据
     */
    const versionData = {
      softwareId: softwareId,

      version: version.trim(),

      versionType: versionType,

      title: title.trim(),

      releaseNotes: releaseNotes.trim(),

      forceUpdate: forceUpdate,

      allowDownload: allowDownload,
    };

    try {
      let response: Response;

      /**
       * 新增
       */
      if (editingVersionId === null) {
        response = await apiFetch("/api/softwareversions", {
          method: "POST",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify(versionData),
        });
      } else {
        /**
         * 编辑
         */
        response = await apiFetch(`/api/softwareversions/${editingVersionId}`, {
          method: "PUT",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify(versionData),
        });
      }

      /**
       * 后端返回错误
       */
      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `保存失败：${response.status}`);
      }

      /**
       * 保存成功
       */
      clearVersionForm();

      setShowVersionForm(false);

      /**
       * 重新请求数据库，
       * 刷新页面上的版本列表。
       */
      await loadVersions();
    } catch (error) {
      console.error("保存版本失败：", error);

      alert("保存版本失败：" + String(error));
    }
  }

  /**
   * 编辑某个版本。
   *
   * 把当前版本的数据填充到表单。
   */
  function editVersion(softwareVersion: SoftwareVersion) {
    setEditingVersionId(softwareVersion.id);

    setSoftwareId(softwareVersion.softwareId);

    setVersion(softwareVersion.version);

    setVersionType(softwareVersion.versionType);

    setTitle(softwareVersion.title);

    setReleaseNotes(softwareVersion.releaseNotes);

    setForceUpdate(softwareVersion.forceUpdate);

    setAllowDownload(softwareVersion.allowDownload);

    setShowVersionForm(true);
  }

  /**
   * 给某个软件版本上传安装包
   */
  /**
   * 上传安装包
   */
  async function uploadPackage() {
    /**
     * 必须先选择一个版本
     */
    if (selectedUploadVersion === null) {
      alert("请先选择要上传安装包的版本");
      return;
    }

    /**
     * 必须选择文件
     */
    if (selectedPackage === null) {
      alert("请先选择安装包");
      return;
    }

    /**
     * 当前版本ID
     */
    const softwareVersionId = selectedUploadVersion.id;

    try {
      setUploadingVersionId(softwareVersionId);

      setUploadProgress(0);
      setIsServerProcessing(false);

      await uploadPackageWithProgress(softwareVersionId, selectedPackage);

      setUploadProgress(100);

      alert("安装包上传成功");

      setSelectedPackage(null);
      setSelectedUploadVersion(null);

      await loadVersions();
    } catch (error) {
      console.error("上传安装包失败：", error);

      alert("上传安装包失败：" + String(error));
    } finally {
      setUploadingVersionId(null);
      setIsServerProcessing(false);
    }
  }
  /**
   * 打开安装包上传区域
   */
  function openPackageUpload(softwareVersion: SoftwareVersion) {
    if (softwareVersion.publishStatus !== "Draft") {
      alert("只有草稿版本可以上传或更换安装包");
      return;
    }

    setSelectedUploadVersion(softwareVersion);

    setSelectedPackage(null);
  }

  /**
   * 打开某个版本的“资料管理”窗口。
   */
  async function openVersionAttachments(softwareVersion: SoftwareVersion) {
    /*
     * 记录当前正在管理哪个版本。
     */
    setSelectedAttachmentVersion(softwareVersion);

    /*
     * 每次打开都清空上一次输入。
     */
    setSelectedAttachmentFile(null);

    setAttachmentType("Manual");

    setAttachmentCustomerVisible(true);

    setAttachmentRemark("");

    setAttachmentFileInputKey((value) => value + 1);

    /*
     * 加载这个版本已有的资料。
     */
    await loadVersionAttachments(softwareVersion.id);
  }

  async function confirmPublishVersion() {
    if (publishingVersion === null) {
      return;
    }

    if (!publishToAll && selectedCustomerIds.length === 0) {
      alert("请选择至少一个发布客户");
      return;
    }

    try {
      setIsPublishing(true);

      const response = await apiFetch(
        `/api/softwareversions/${publishingVersion.id}/publish`,
        {
          method: "POST",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            publishToAll,
            customerIds: selectedCustomerIds,
          }),
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `发布失败：${response.status}`);
      }

      setPublishingVersion(null);

      setPublishCustomers([]);

      setSelectedCustomerIds([]);

      await loadVersions();
    } catch (error) {
      console.error("发布版本失败：", error);

      alert("发布版本失败：" + String(error));
    } finally {
      setIsPublishing(false);
    }
  }
  function togglePublishCustomer(customerId: number) {
    setSelectedCustomerIds((current) =>
      current.includes(customerId)
        ? current.filter((id) => id !== customerId)
        : [...current, customerId],
    );
  }
  /**
   * 查询某个版本已有的附加资料。
   */
  async function loadVersionAttachments(softwareVersionId: number) {
    try {
      const response = await apiFetch(
        `/api/software-versions/${softwareVersionId}/attachments`,
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `加载版本资料失败：${response.status}`);
      }

      const data = (await response.json()) as VersionAttachment[];

      setVersionAttachments(data);
    } catch (error) {
      console.error("加载版本资料失败：", error);

      alert("加载版本资料失败：" + String(error));
    }
  } /**
   * 上传版本附加资料。
   */
  async function uploadVersionAttachment() {
    /*
     * 必须先选择版本。
     */
    if (selectedAttachmentVersion === null) {
      return;
    }

    /*
     * 必须选择文件。
     */
    if (selectedAttachmentFile === null) {
      alert("请选择需要上传的资料");

      return;
    }

    /*
     * 前端也先检查100MB。
     *
     * 后端仍然会再次检查，
     * 所以前端检查只是改善用户体验。
     */
    const maxFileSize = 100 * 1024 * 1024;

    if (selectedAttachmentFile.size > maxFileSize) {
      alert("单个附件不能超过100MB");

      return;
    }

    try {
      setIsUploadingAttachment(true);

      const formData = new FormData();

      formData.append("file", selectedAttachmentFile);

      formData.append("attachmentType", attachmentType);

      /*
       * 问题日志永远不能给客户看。
       *
       * 前端这样控制只是方便用户，
       * 后端同样已经强制控制。
       */
      formData.append(
        "isCustomerVisible",
        attachmentType === "Log" ? "false" : String(attachmentCustomerVisible),
      );

      formData.append("remark", attachmentRemark.trim());

      const response = await apiFetch(
        `/api/software-versions/${selectedAttachmentVersion.id}/attachments`,
        {
          method: "POST",

          /*
           * FormData 不要自己设置
           * Content-Type。
           *
           * 浏览器会自动生成 boundary。
           */
          body: formData,
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `资料上传失败：${response.status}`);
      }

      /*
       * 上传成功以后清空表单。
       */
      setSelectedAttachmentFile(null);

      setAttachmentRemark("");

      setAttachmentFileInputKey((value) => value + 1);

      /*
       * 重新加载附件列表。
       */
      await loadVersionAttachments(selectedAttachmentVersion.id);
    } catch (error) {
      console.error("上传版本资料失败：", error);

      alert("上传版本资料失败：" + String(error));
    } finally {
      setIsUploadingAttachment(false);
    }
  } /**
   * 下载版本附加资料。
   *
   * 不能直接用普通 <a href>，
   * 因为接口需要 JWT。
   *
   * 所以：
   *
   * apiFetch
   * → Blob
   * → 临时URL
   * → 浏览器下载
   */
  async function downloadVersionAttachment(attachment: VersionAttachment) {
    try {
      const response = await apiFetch(
        `/api/software-versions/attachments/${attachment.id}/download`,
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `下载失败：${response.status}`);
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
  } /**
   * 删除版本附加资料。
   */
  async function deleteVersionAttachment(attachment: VersionAttachment) {
    /*
     * 附件删除这里暂时直接使用 confirm。
     *
     * 后面也可以再改成
     * ConfirmDeleteButton。
     */
    const confirmed = window.confirm(
      `确定要删除资料“${attachment.fileName}”吗？`,
    );

    if (!confirmed) {
      return;
    }

    try {
      const response = await apiFetch(
        `/api/software-versions/attachments/${attachment.id}`,
        {
          method: "DELETE",
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `删除失败：${response.status}`);
      }

      /*
       * 删除成功以后重新读取。
       */
      if (selectedAttachmentVersion) {
        await loadVersionAttachments(selectedAttachmentVersion.id);
      }
    } catch (error) {
      console.error("删除版本资料失败：", error);

      alert("删除版本资料失败：" + String(error));
    }
  } /**
   * 把后端保存的英文类型
   * 转换成界面上的中文。
   */
  function getAttachmentTypeName(type: string) {
    switch (type) {
      case "Manual":
        return "用户手册";

      case "ReleaseDocument":
        return "版本说明";

      case "Troubleshooting":
        return "常见问题";

      case "Log":
        return "问题日志";

      case "Config":
        return "配置文件";

      default:
        return "其他资料";
    }
  }
  /**
   * 删除软件版本
   *
   * 删除确认已经由 ConfirmDeleteButton 负责，
   * 所以这里不需要再调用 window.confirm()。
   */
  async function deleteVersion(id: number) {
    try {
      const response = await apiFetch(`/api/softwareversions/${id}`, {
        method: "DELETE",
      });

      // 后端删除失败
      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `删除版本失败：${response.status}`);
      }

      // 删除成功以后重新加载版本列表
      await loadVersions();
    } catch (error) {
      console.error("删除版本失败：", error);

      alert("删除版本失败：" + String(error));
    }
  }

   
  async function deprecateVersion(softwareVersion: SoftwareVersion) {
    const confirmed = window.confirm(
      `确定停用版本 ${softwareVersion.version} 吗？停用后客户将不能继续下载。`,
    );

    if (!confirmed) {
      return;
    }

    try {
      const response = await apiFetch(
        `/api/softwareversions/${softwareVersion.id}/deprecate`,
        {
          method: "POST",
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `停用失败：${response.status}`);
      }

      await loadVersions();
    } catch (error) {
      console.error("停用版本失败：", error);

      alert("停用版本失败：" + String(error));
    }
  }
  /**
   * 把字节转换成更容易阅读的文件大小。
   *
   * 例如：
   * 1024       → 1.00 KB
   * 1048576    → 1.00 MB
   */
  function formatFileSize(bytes: number) {
    if (bytes <= 0) {
      return "0 B";
    }

    const kb = 1024;
    const mb = kb * 1024;
    const gb = mb * 1024;

    if (bytes >= gb) {
      return (bytes / gb).toFixed(2) + " GB";
    }

    if (bytes >= mb) {
      return (bytes / mb).toFixed(2) + " MB";
    }

    if (bytes >= kb) {
      return (bytes / kb).toFixed(2) + " KB";
    }

    return bytes + " B";
  }
  function formatDate(dateText: string | null) {
    if (!dateText) {
      return "-";
    }

    return new Date(dateText).toLocaleString();
  }
  /**
   * 下载某个软件版本的安装包
   */
  function downloadPackage(softwareVersion: SoftwareVersion) {
    /**
     * 没有安装包
     */
    if (!softwareVersion.packageFileName) {
      alert("当前版本尚未上传安装包");
      return;
    }
    const software = softwares.find((x) => x.id === softwareVersion.softwareId);

    if (!software) {
      alert("所属软件不存在");
      return;
    }

    if (!software.isEnabled) {
      alert("当前软件已停用");
      return;
    }

    if (!software.allowDownload) {
      alert("当前软件已禁止下载");
      return;
    }

    if (!softwareVersion.allowDownload) {
      alert("当前版本不允许下载");
      return;
    }

    /**
     * 创建一个临时的 <a> 标签。
     *
     * 相当于：
     *
     * <a href="/api/...">
     *   下载
     * </a>
     */
    const link = document.createElement("a");

    link.href = `/api/softwareversions/${softwareVersion.id}/package/download`;

    /*
     * 不需要把这个标签真正显示在页面上。
     *
     * 直接模拟点击即可。
     */
    document.body.appendChild(link);

    link.click();

    document.body.removeChild(link);
  }
  /**
   * 判断某个版本最终是否允许下载
   */
  function canDownloadVersion(softwareVersion: SoftwareVersion) {
    const software = softwares.find((x) => x.id === softwareVersion.softwareId);

    if (!software) {
      return false;
    }

    return (
      software.isEnabled &&
      software.allowDownload &&
      softwareVersion.allowDownload
    );
  }
  function getPublishStatusName(status: string) {
    switch (status) {
      case "Draft":
        return "草稿";

      case "Published":
        return "已发布";

      case "Deprecated":
        return "已停用";

      default:
        return status;
    }
  }
  return (
    <div className="content">
      {/* ================= 页面标题 ================= */}
      <PageHeader
        title="版本管理"
        buttonText="+ 新增版本"
        onButtonClick={() => {
          clearVersionForm();

          setShowVersionForm(true);
        }}
      />
      {/* =========================
    安装包上传区域
    ========================= */}
      {selectedUploadVersion !== null && (
        <div className="form-box">
          <h3>上传安装包</h3>

          <div className="form-section">
            <h4>当前版本</h4>

            <div className="form-grid">
              {/* 软件 */}
              <div className="form-item">
                <label>软件名称</label>

                <div>{getSoftwareName(selectedUploadVersion.softwareId)}</div>
              </div>

              {/* 版本 */}
              <div className="form-item">
                <label>版本号</label>

                <div>{selectedUploadVersion.version}</div>
              </div>

              {/* 类型 */}
              <div className="form-item">
                <label>版本类型</label>

                <div>{selectedUploadVersion.versionType}</div>
              </div>

              {/* 当前安装包 */}
              <div className="form-item">
                <label>当前安装包</label>

                <div>{selectedUploadVersion.packageFileName || "暂未上传"}</div>
              </div>
            </div>
          </div>

          {/* =========================
        文件选择
        ========================= */}
          <div className="form-section">
            <h4>选择安装包</h4>

            <div className="form-item">
              <input
                type="file"
                accept=".exe,.msi,.zip"
                onChange={(e) => {
                  const file = e.target.files?.[0];

                  if (file) {
                    setSelectedPackage(file);
                  } else {
                    setSelectedPackage(null);
                  }
                }}
              />
            </div>

            {/* 选择文件以后显示文件信息 */}
            {selectedPackage !== null && (
              <div className="package-file-info">
                <div>
                  文件名：
                  {selectedPackage.name}
                </div>

                <div>
                  文件大小：
                  {formatFileSize(selectedPackage.size)}
                </div>
              </div>
            )}
          </div>

          {/* =========================
        按钮
        ========================= */}
          <div className="form-buttons">
            {uploadingVersionId !== null && (
              <div className="package-upload-progress">
                <div className="package-upload-progress-info">
                  <span>
                    {isServerProcessing
                      ? "服务器正在处理安装包..."
                      : "正在上传安装包"}
                  </span>

                  <span>{uploadProgress}%</span>
                </div>

                <div className="package-upload-progress-track">
                  <div
                    className="package-upload-progress-bar"
                    style={{
                      width: `${uploadProgress}%`,
                    }}
                  />
                </div>
              </div>
            )}
            <button
              className="primary-button"
              disabled={uploadingVersionId === selectedUploadVersion.id}
              onClick={uploadPackage}
            >
              {uploadingVersionId === selectedUploadVersion.id
                ? "上传中..."
                : "开始上传"}
            </button>

            <button
              className="normal-button"
              disabled={uploadingVersionId !== null}
              onClick={() => {
                setSelectedPackage(null);
                setSelectedUploadVersion(null);
              }}
            >
              取消
            </button>
          </div>
        </div>
      )}

      {showVersionForm && (
        <div className="form-box">
          <h3>{editingVersionId === null ? "新增版本" : "编辑版本"}</h3>

          {/* ================= 基本信息 ================= */}
          <div className="form-section">
            <h4>基本信息</h4>

            <div className="form-grid">
              {/* 所属软件 */}
              <div className="form-item">
                <label>所属软件 *</label>

                <select
                  value={softwareId}
                  onChange={(e) => setSoftwareId(Number(e.target.value))}
                >
                  <option value={0}>请选择软件</option>

                  {softwares.map((software) => (
                    <option key={software.id} value={software.id}>
                      {software.name}
                    </option>
                  ))}
                </select>
              </div>

              {/* 版本号 */}
              <div className="form-item">
                <label>版本号 *</label>

                <input
                  type="text"
                  placeholder="例如：1.0.0"
                  value={version}
                  onChange={(e) => setVersion(e.target.value)}
                />
              </div>

              {/* 版本类型 */}
              <div className="form-item">
                <label>版本类型</label>

                <select
                  value={versionType}
                  onChange={(e) => setVersionType(e.target.value)}
                >
                  <option value="Release">Release</option>

                  <option value="Beta">Beta</option>

                  <option value="Dev">Dev</option>
                </select>
              </div>

              {/* 版本标题 */}
              <div className="form-item">
                <label>版本标题</label>

                <input
                  type="text"
                  placeholder="例如：2026年9月正式版本"
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                />
              </div>
            </div>
          </div>

          {/* ================= 发布设置 ================= */}
          <div className="form-section">
            <h4>发布设置</h4>

            <div className="checkbox-row">
              <label>
                <input
                  type="checkbox"
                  checked={forceUpdate}
                  onChange={(e) => setForceUpdate(e.target.checked)}
                />
                强制升级
              </label>

              <label>
                <input
                  type="checkbox"
                  checked={allowDownload}
                  onChange={(e) => setAllowDownload(e.target.checked)}
                />
                允许下载
              </label>
            </div>
          </div>

          {/* ================= 更新说明 ================= */}
          <div className="form-section">
            <h4>更新说明</h4>

            <div className="form-item form-item-full">
              <textarea
                className="remark-input"
                placeholder="请输入本版本的更新内容"
                value={releaseNotes}
                onChange={(e) => setReleaseNotes(e.target.value)}
              />
            </div>
          </div>

          {/* ================= 表单按钮 ================= */}
          <div className="form-buttons">
            <button className="primary-button" onClick={saveVersion}>
              {editingVersionId === null ? "新增" : "保存"}
            </button>

            <button
              className="normal-button"
              onClick={() => {
                clearVersionForm();
                setShowVersionForm(false);
              }}
            >
              取消
            </button>
          </div>
        </div>
      )}
      {/* ================= 搜索栏 ================= */}
      <SearchBar
        value={searchKeyword}
        placeholder="搜索软件名称、版本号、版本类型或版本标题"
        onChange={setSearchKeyword}
        onClear={() => setSearchKeyword("")}
      />

      {/* ================= 版本表格 ================= */}
      <div className="version-table">
        {/* 表头 */}
        <div className="version-table-header">
          <div>软件名称</div>
          <div>版本号</div>
          <div>版本类型</div>
          <div>版本标题</div>
          <div>发布状态</div>
          <div>强制升级</div>
          <div>允许下载</div>

          <div>安装包</div>

          <div>操作</div>
        </div>

        {/* 数据行 */}
        {filteredVersions.map((version) => (
          <div className="version-table-row" key={version.id}>
            <div>{getSoftwareName(version.softwareId)}</div>

            <div>{version.version}</div>

            <div>{version.versionType}</div>

            <div>{version.title || "-"}</div>

            <div>{getPublishStatusName(version.publishStatus)}</div>

            <div>{version.forceUpdate ? "是" : "否"}</div>

            <div>{version.allowDownload ? "是" : "否"}</div>
            <div>
              {version.packageFileName ? (
                <>
                  <div>{version.packageFileName}</div>

                  <div className="package-size">
                    {formatFileSize(version.packageFileSize)}
                  </div>
                </>
              ) : (
                "未上传"
              )}
            </div>
            {/* 版本附加资料 */}

            <div className="table-actions">
              {/* 编辑版本 */}

              <button
                className="normal-button"
                onClick={() => openVersionAttachments(version)}
              >
                资料
              </button>
              {version.publishStatus !== "Draft" && (
                <button
                  type="button"
                  className="normal-button"
                  onClick={() => void openPublishedCustomers(version)}
                >
                  查看发布客户
                </button>
              )}
              {version.publishStatus === "Draft" && (
                <button
                  className="edit-button"
                  onClick={() => editVersion(version)}
                >
                  编辑
                </button>
              )}
              {version.publishStatus === "Draft" &&
                version.versionType !== "Dev" && (
                  <button
                    type="button"
                    className="primary-button"
                    onClick={() => void openPublishDialog(version)}
                  >
                    发布
                  </button>
                )}

              {version.publishStatus === "Published" && (
                <button
                  className="normal-button"
                  onClick={() => void deprecateVersion(version)}
                >
                  停用
                </button>
              )}

              {/* 删除版本 */}

              {version.publishStatus === "Draft" && (
                <ConfirmDeleteButton
                  message={`确定要删除版本 ${version.version} 吗？`}
                  onConfirm={() => deleteVersion(version.id)}
                />
              )}

              {/* =========================
      选择安装包
      ========================= */}
              {version.publishStatus === "Draft" && (
                <button
                  className="normal-button"
                  onClick={() => openPackageUpload(version)}
                >
                  {version.packageFileName ? "更换安装包" : "上传安装包"}
                </button>
              )}
              {version.packageFileName && (
                <button
                  className="normal-button"
                  disabled={!canDownloadVersion(version)}
                  onClick={() => downloadPackage(version)}
                >
                  下载
                </button>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* 没有数据 */}
      {filteredVersions.length === 0 && (
        <div className="empty">暂无版本数据</div>
      )}

      {selectedAttachmentVersion && (
        <div className="version-attachment-mask">
          <div className="version-attachment-dialog">
            {/* ================= 标题 ================= */}
            <div className="version-attachment-header">
              <div>
                <h3>版本资料管理</h3>

                <p>
                  {getSoftwareName(selectedAttachmentVersion.softwareId)}

                  {" · "}

                  {selectedAttachmentVersion.version}
                </p>
              </div>

              <button
                type="button"
                className="normal-button"
                onClick={() => setSelectedAttachmentVersion(null)}
              >
                关闭
              </button>
            </div>

            {/* ================= 上传区域 ================= */}
            <div className="version-attachment-upload">
              <h4>上传资料</h4>

              <div className="version-attachment-form">
                {/* 文件 */}
                <div className="form-item">
                  <label>文件</label>

                  <input
                    key={attachmentFileInputKey}
                    type="file"
                    onChange={(e) =>
                      setSelectedAttachmentFile(e.target.files?.[0] ?? null)
                    }
                  />
                </div>

                {/* 类型 */}
                <div className="form-item">
                  <label>类型</label>

                  <select
                    value={attachmentType}
                    onChange={(e) => {
                      const value = e.target.value;

                      setAttachmentType(value);

                      /*
                       * 选择“问题日志”以后，
                       * 自动切换为客户不可见。
                       */
                      if (value === "Log") {
                        setAttachmentCustomerVisible(false);
                      }
                    }}
                  >
                    <option value="Manual">用户手册</option>

                    <option value="ReleaseDocument">版本说明</option>

                    <option value="Troubleshooting">常见问题</option>

                    <option value="Log">问题日志</option>

                    <option value="Config">配置文件</option>

                    <option value="Other">其他资料</option>
                  </select>
                </div>

                {/* 客户可见 */}
                <div className="form-item">
                  <label>客户可见</label>

                  <input
                    type="checkbox"
                    checked={attachmentCustomerVisible}
                    disabled={attachmentType === "Log"}
                    onChange={(e) =>
                      setAttachmentCustomerVisible(e.target.checked)
                    }
                  />
                </div>

                {/* 备注 */}
                <div className="form-item">
                  <label>备注</label>

                  <input
                    type="text"
                    value={attachmentRemark}
                    placeholder="选填"
                    onChange={(e) => setAttachmentRemark(e.target.value)}
                  />
                </div>
              </div>

              {attachmentType === "Log" && (
                <div className="version-attachment-tip">
                  问题日志属于内部资料， 客户不可见。
                </div>
              )}

              <button
                type="button"
                className="primary-button"
                disabled={isUploadingAttachment}
                onClick={uploadVersionAttachment}
              >
                {isUploadingAttachment ? "正在上传..." : "上传资料"}
              </button>
            </div>

            {/* ================= 已有资料 ================= */}
            <div className="version-attachment-list">
              <h4>已有资料</h4>

              {versionAttachments.length === 0 ? (
                <div className="empty-text">暂无附加资料</div>
              ) : (
                <table>
                  <thead>
                    <tr>
                      <th>文件</th>

                      <th>类型</th>

                      <th>大小</th>

                      <th>客户可见</th>

                      <th>备注</th>

                      <th>上传人</th>

                      <th>操作</th>
                    </tr>
                  </thead>

                  <tbody>
                    {versionAttachments.map((attachment) => (
                      <tr key={attachment.id}>
                        <td>{attachment.fileName}</td>

                        <td>
                          {getAttachmentTypeName(attachment.attachmentType)}
                        </td>

                        <td>{formatFileSize(attachment.fileSize)}</td>

                        <td>{attachment.isCustomerVisible ? "是" : "否"}</td>

                        <td>{attachment.remark || "-"}</td>

                        <td>{attachment.uploadedByName || "-"}</td>

                        <td>
                          <div className="table-actions">
                            <button
                              type="button"
                              className="normal-button"
                              onClick={() =>
                                downloadVersionAttachment(attachment)
                              }
                            >
                              下载
                            </button>

                            <button
                              type="button"
                              className="delete-button"
                              onClick={() =>
                                deleteVersionAttachment(attachment)
                              }
                            >
                              删除
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          </div>
        </div>
      )}

      {publishingVersion && (
        <div className="version-attachment-mask">
          <div className="version-attachment-dialog">
            <div className="version-attachment-header">
              <div>
                <h3>发布版本</h3>

                <p>
                  {getSoftwareName(publishingVersion.softwareId)}
                  {" · "}
                  {publishingVersion.version}
                  {" · "}
                  {publishingVersion.versionType}
                </p>
              </div>

              <button
                type="button"
                className="normal-button"
                disabled={isPublishing}
                onClick={() => setPublishingVersion(null)}
              >
                关闭
              </button>
            </div>

            <div className="form-section">
              <h4>发布范围</h4>

              <label>
                <input
                  type="radio"
                  checked={publishToAll}
                  disabled={publishingVersion.versionType === "Beta"}
                  onChange={() => setPublishToAll(true)}
                />
                全部授权客户 （{publishCustomers.length} 家）
              </label>

              <br />

              <label>
                <input
                  type="radio"
                  checked={!publishToAll}
                  onChange={() => setPublishToAll(false)}
                />
                指定客户
              </label>
            </div>

            {!publishToAll && (
              <div className="form-section">
                <h4>选择客户</h4>

                {publishCustomers.length === 0 ? (
                  <div className="empty">当前没有已授权客户</div>
                ) : (
                  publishCustomers.map((customer) => (
                    <label
                      key={customer.customerId}
                      style={{
                        display: "block",
                        marginBottom: 10,
                      }}
                    >
                      <input
                        type="checkbox"
                        checked={selectedCustomerIds.includes(
                          customer.customerId,
                        )}
                        onChange={() =>
                          togglePublishCustomer(customer.customerId)
                        }
                      />{" "}
                      {customer.name}
                      {" · "}
                      {customer.code}
                      {(customer.province || customer.city) && (
                        <>
                          {" · "}
                          {customer.province}
                          {customer.city}
                        </>
                      )}
                    </label>
                  ))
                )}
              </div>
            )}

            {publishingVersion.versionType === "Beta" && (
              <div className="form-section">Beta 版本只能发布给指定客户。</div>
            )}

            <div className="form-buttons">
              <button
                className="primary-button"
                disabled={isPublishing}
                onClick={() => void confirmPublishVersion()}
              >
                {isPublishing ? "发布中..." : "确认发布"}
              </button>

              <button
                className="normal-button"
                disabled={isPublishing}
                onClick={() => setPublishingVersion(null)}
              >
                取消
              </button>
            </div>
          </div>
        </div>
      )}
      {publishedCustomerData && (
        <div className="version-attachment-mask">
          <div className="version-attachment-dialog">
            <div className="version-attachment-header">
              <div>
                <h3>版本发布客户</h3>

                <p>
                  {publishedCustomerData.version}
                  {" · "}
                  {publishedCustomerData.versionType}
                </p>
              </div>

              <button
                type="button"
                className="normal-button"
                onClick={() => setPublishedCustomerData(null)}
              >
                关闭
              </button>
            </div>

            {publishedCustomerLoading ? (
              <div
                style={{
                  padding: "20px 0",
                }}
              >
                正在加载发布客户...
              </div>
            ) : (
              <>
                <div
                  style={{
                    marginBottom: 16,
                  }}
                >
                  已发布给
                  <strong> {publishedCustomerData.customerCount} </strong>
                  家客户
                </div>

                {publishedCustomerData.customers.length === 0 ? (
                  <div className="empty">暂无发布客户记录</div>
                ) : (
                  <div>
                    {publishedCustomerData.customers.map((customer) => (
                      <div
                        key={customer.customerId}
                        className="published-customer-item"
                      >
                        <div>
                          <strong>{customer.name}</strong>

                          <div>{customer.code}</div>
                        </div>

                        <div>
                          <div>
                            {customer.province}
                            {customer.city}
                          </div>

                          <div>
                            发布于：
                            {formatDate(customer.publishedToCustomerAt)}
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default VersionPage;
