import { useEffect, useState } from "react";

import PageHeader from "../components/PageHeader";
import SearchBar from "../components/SearchBar";

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
   * 是否已经发布
   */
  const [isPublished, setIsPublished] = useState(false);

  /**
   * 是否强制升级
   */
  const [forceUpdate, setForceUpdate] = useState(false);

  /**
   * 是否允许下载
   */
  const [allowDownload, setAllowDownload] = useState(true);
  /**
   * 从后端加载软件列表
   */
  async function loadSoftwares() {
    try {
      const response = await fetch("/api/softwares");

      if (!response.ok) {
        throw new Error(`获取软件列表失败：${response.status}`);
      }

      const data: Software[] = await response.json();

      setSoftwares(data);
    } catch (error) {
      console.error("获取软件列表失败：", error);
    }
  }

  /**
   * 从后端加载版本列表
   */
  async function loadVersions() {
    try {
      const response = await fetch("/api/softwareversions");

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
    setIsPublished(false);
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

      isPublished: isPublished,

      forceUpdate: forceUpdate,

      allowDownload: allowDownload,
    };

    try {
      let response: Response;

      /**
       * 新增
       */
      if (editingVersionId === null) {
        response = await fetch("/api/softwareversions", {
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
        response = await fetch(`/api/softwareversions/${editingVersionId}`, {
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

    setIsPublished(softwareVersion.isPublished);

    setForceUpdate(softwareVersion.forceUpdate);

    setAllowDownload(softwareVersion.allowDownload);

    setShowVersionForm(true);
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
                  checked={isPublished}
                  onChange={(e) => setIsPublished(e.target.checked)}
                />
                已发布
              </label>

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
          <div>操作</div>
        </div>

        {/* 数据行 */}
        {filteredVersions.map((version) => (
          <div className="version-table-row" key={version.id}>
            <div>{getSoftwareName(version.softwareId)}</div>

            <div>{version.version}</div>

            <div>{version.versionType}</div>

            <div>{version.title || "-"}</div>

            <div>{version.isPublished ? "已发布" : "未发布"}</div>

            <div>{version.forceUpdate ? "是" : "否"}</div>

            <div>{version.allowDownload ? "是" : "否"}</div>

            <div className="table-actions">
              {/* 下一步实现 */}
              <button
                className="edit-button"
                onClick={() => editVersion(version)}
              >
                编辑
              </button>
            </div>
          </div>
        ))}
      </div>

      {/* 没有数据 */}
      {filteredVersions.length === 0 && (
        <div className="empty">暂无版本数据</div>
      )}
    </div>
  );
}

export default VersionPage;
