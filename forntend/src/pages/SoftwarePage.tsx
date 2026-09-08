import { useEffect, useState } from "react";
import StatusBadge from "../components/StatusBadge";
import ConfirmDeleteButton from "../components/ConfirmDeleteButton";
import PageHeader from "../components/PageHeader";
import SearchBar from "../components/SearchBar";
/**
 * 软件数据类型
 *
 * 这里的字段和后端 Models/Software.cs 基本一一对应。
 *
 * C# 后端：
 *     Name
 *     Code
 *     ShortName
 *
 * JSON 返回到 React 后默认变成：
 *     name
 *     code
 *     shortName
 */
interface Software {
  id: number;

  // 软件基本信息
  name: string;
  code: string;
  shortName: string;
  category: string;
  description: string;

  // 软件负责人信息
  developer: string;
  supportOwner: string;
  department: string;

  // 技术信息
  platform: string;
  technologyStack: string;

  // 软件状态
  isEnabled: boolean;
  allowDownload: boolean;

  // 备注
  remark: string;

  // 创建、更新时间由后端维护
  createdAt: string;
  updatedAt: string;
}

function SoftwarePage() {
  /**
   * =========================
   * 一、页面数据状态
   * =========================
   */

  // 后端加载回来的完整软件列表
  const [softwares, setSoftwares] = useState<Software[]>([]);

  // 是否显示新增/编辑表单
  const [showSoftwareForm, setShowSoftwareForm] = useState(false);

  /**
   * 正在编辑的软件ID
   *
   * null：
   *     当前是“新增软件”
   *
   * 例如 3：
   *     当前正在编辑 Id=3 的软件
   */
  const [editingSoftwareId, setEditingSoftwareId] = useState<number | null>(
    null,
  );

  // 搜索框内容
  const [searchKeyword, setSearchKeyword] = useState("");

  /**
   * =========================
   * 二、软件表单字段
   * =========================
   */

  const [softwareName, setSoftwareName] = useState("");
  const [softwareCode, setSoftwareCode] = useState("");
  const [shortName, setShortName] = useState("");
  const [category, setCategory] = useState("");

  const [description, setDescription] = useState("");

  const [developer, setDeveloper] = useState("");
  const [supportOwner, setSupportOwner] = useState("");
  const [department, setDepartment] = useState("");

  const [platform, setPlatform] = useState("");
  const [technologyStack, setTechnologyStack] = useState("");

  const [isEnabled, setIsEnabled] = useState(true);
  const [allowDownload, setAllowDownload] = useState(true);

  const [remark, setRemark] = useState("");

  /**
   * =========================
   * 三、加载软件列表
   * =========================
   *
   * 请求：
   * GET /api/softwares
   *
   * 后端返回 Software[]
   */
  async function loadSoftwares() {
    try {
      const response = await fetch("/api/softwares");

      // HTTP 不是 2xx 时认为请求失败
      if (!response.ok) {
        throw new Error(`加载失败：${response.status}`);
      }

      // 把后端 JSON 转换成 Software 数组
      const data: Software[] = await response.json();

      // 保存到 React 状态中
      setSoftwares(data);
    } catch (error) {
      console.error("加载软件列表失败：", error);
    }
  }

  /**
   * 页面第一次打开时加载软件列表。
   *
   * [] 表示这里只在组件第一次显示时执行一次。
   */
  useEffect(() => {
    loadSoftwares();
  }, []);

  /**
   * =========================
   * 四、清空软件表单
   * =========================
   *
   * 新增、取消、保存完成以后都会用到，
   * 避免每个地方重复写十几次 setXXX。
   */
  function clearSoftwareForm() {
    setSoftwareName("");
    setSoftwareCode("");
    setShortName("");
    setCategory("");

    setDescription("");

    setDeveloper("");
    setSupportOwner("");
    setDepartment("");

    setPlatform("");
    setTechnologyStack("");

    // 新增软件时默认启用、允许下载
    setIsEnabled(true);
    setAllowDownload(true);

    setRemark("");
  }

  /**
   * =========================
   * 五、打开新增软件表单
   * =========================
   */
  function createSoftware() {
    // null 表示新增模式
    setEditingSoftwareId(null);

    // 防止上一次编辑的数据残留
    clearSoftwareForm();

    // 显示表单
    setShowSoftwareForm(true);
  }

  /**
   * =========================
   * 六、打开编辑软件表单
   * =========================
   *
   * 把当前软件的数据全部填入对应 useState，
   * 页面中的 input 就会自动显示这些数据。
   */
  function editSoftware(software: Software) {
    setEditingSoftwareId(software.id);

    setSoftwareName(software.name);
    setSoftwareCode(software.code);
    setShortName(software.shortName);
    setCategory(software.category);

    setDescription(software.description);

    setDeveloper(software.developer);
    setSupportOwner(software.supportOwner);
    setDepartment(software.department);

    setPlatform(software.platform);
    setTechnologyStack(software.technologyStack);

    setIsEnabled(software.isEnabled);
    setAllowDownload(software.allowDownload);

    setRemark(software.remark);

    setShowSoftwareForm(true);
  }

  /**
   * =========================
   * 七、保存软件
   * =========================
   *
   * editingSoftwareId == null
   *     POST 新增
   *
   * editingSoftwareId != null
   *     PUT 修改
   */
  async function saveSoftware() {
    /**
     * 前端先做最基础校验。
     *
     * 后端同样也会校验，
     * 所以前端校验主要是为了提升用户体验。
     */

    if (softwareName.trim() === "") {
      alert("软件名称不能为空");
      return;
    }

    if (softwareCode.trim() === "") {
      alert("软件编码不能为空");
      return;
    }

    try {
      let response: Response;

      /**
       * -------------------------
       * 新增软件
       * -------------------------
       */
      if (editingSoftwareId === null) {
        response = await fetch("/api/softwares", {
          method: "POST",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            name: softwareName,
            code: softwareCode,
            shortName: shortName,
            category: category,

            description: description,

            developer: developer,
            supportOwner: supportOwner,
            department: department,

            platform: platform,
            technologyStack: technologyStack,

            isEnabled: isEnabled,
            allowDownload: allowDownload,

            remark: remark,
          }),
        });
      } else {
        /**
         * -------------------------
         * 修改软件
         * -------------------------
         *
         * 例如：
         *
         * PUT /api/softwares/3
         */

        response = await fetch(`/api/softwares/${editingSoftwareId}`, {
          method: "PUT",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            id: editingSoftwareId,

            name: softwareName,
            code: softwareCode,
            shortName: shortName,
            category: category,

            description: description,

            developer: developer,
            supportOwner: supportOwner,
            department: department,

            platform: platform,
            technologyStack: technologyStack,

            isEnabled: isEnabled,
            allowDownload: allowDownload,

            remark: remark,
          }),
        });
      }

      /**
       * 保存失败
       *
       * 比如后端可能返回：
       *
       * 400
       * 软件编码已存在
       */
      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `HTTP错误：${response.status}`);
      }

      /**
       * 保存成功以后：
       *
       * 1. 清空表单
       * 2. 清除编辑状态
       * 3. 关闭表单
       * 4. 重新从数据库加载软件
       */

      clearSoftwareForm();

      setEditingSoftwareId(null);

      setShowSoftwareForm(false);

      await loadSoftwares();
    } catch (error) {
      console.error("保存软件失败：", error);

      alert("保存软件失败：" + String(error));
    }
  }

  /**
   * =========================
   * 八、删除软件
   * =========================
   *
   * 请求：
   *
   * DELETE /api/softwares/{id}
   */
  async function deleteSoftware(id: number) {
    // 删除属于危险操作，先让用户确认

    try {
      const response = await fetch(`/api/softwares/${id}`, {
        method: "DELETE",
      });

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `删除失败：${response.status}`);
      }

      // 删除成功后重新加载软件列表
      await loadSoftwares();
    } catch (error) {
      console.error("删除软件失败：", error);

      alert("删除软件失败：" + String(error));
    }
  }

  /**
   * =========================
   * 九、软件搜索
   * =========================
   *
   * softwares：
   *     后端返回的完整软件列表
   *
   * filteredSoftwares：
   *     根据搜索关键字过滤后的列表
   *
   * 不直接修改 softwares，
   * 避免搜索后把原始数据丢失。
   */
  const filteredSoftwares = softwares.filter((software) => {
    // 去掉首尾空格并转成小写
    const keyword = searchKeyword.trim().toLowerCase();

    // 没输入内容时显示全部软件
    if (keyword === "") {
      return true;
    }

    /**
     * 以下任意一个字段匹配，
     * 当前软件都会保留下来。
     */
    return (
      software.name.toLowerCase().includes(keyword) ||
      software.code.toLowerCase().includes(keyword) ||
      software.shortName.toLowerCase().includes(keyword) ||
      software.category.toLowerCase().includes(keyword) ||
      software.developer.toLowerCase().includes(keyword) ||
      software.supportOwner.toLowerCase().includes(keyword)
    );
  });

  /**
   * =========================
   * 十、页面
   * =========================
   */
  return (
    <div className="content">
      {/* ======================
          页面标题
         ====================== */}
      <PageHeader
        title="软件管理"
        buttonText="+ 新增软件"
        onButtonClick={createSoftware}
      />

      {/* ======================
          新增 / 编辑表单
         ====================== */}
      {showSoftwareForm && (
        <div className="form-box">
          <h3>{editingSoftwareId === null ? "新增软件" : "编辑软件"}</h3>

          {/* ------------------
              基本信息
             ------------------ */}
          <div className="form-section">
            <h4>基本信息</h4>

            <div className="form-grid">
              <div className="form-item">
                <label>软件编码：</label>

                <input
                  placeholder="例如：ROAD_PROCESS"
                  value={softwareCode}
                  onChange={(e) => setSoftwareCode(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>软件名称：</label>

                <input
                  placeholder="请输入软件名称"
                  value={softwareName}
                  onChange={(e) => setSoftwareName(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>软件简称：</label>

                <input
                  placeholder="请输入软件简称"
                  value={shortName}
                  onChange={(e) => setShortName(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>软件分类：</label>

                <input
                  placeholder="例如：道路检测"
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                />
              </div>
            </div>
          </div>

          {/* ------------------
              负责人信息
             ------------------ */}
          <div className="form-section">
            <h4>负责人信息</h4>

            <div className="form-grid">
              <div className="form-item">
                <label>开发负责人：</label>

                <input
                  placeholder="请输入开发负责人"
                  value={developer}
                  onChange={(e) => setDeveloper(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>售后负责人：</label>

                <input
                  placeholder="请输入售后负责人"
                  value={supportOwner}
                  onChange={(e) => setSupportOwner(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>所属部门：</label>

                <input
                  placeholder="例如：研发部"
                  value={department}
                  onChange={(e) => setDepartment(e.target.value)}
                />
              </div>
            </div>
          </div>

          {/* ------------------
              技术信息
             ------------------ */}
          <div className="form-section">
            <h4>技术信息</h4>

            <div className="form-grid">
              <div className="form-item">
                <label>运行平台：</label>

                <input
                  placeholder="例如：Windows"
                  value={platform}
                  onChange={(e) => setPlatform(e.target.value)}
                />
              </div>

              <div className="form-item">
                <label>技术栈：</label>

                <input
                  placeholder="例如：C++ / Qt"
                  value={technologyStack}
                  onChange={(e) => setTechnologyStack(e.target.value)}
                />
              </div>
            </div>
          </div>

          {/* ------------------
              软件状态
             ------------------ */}
          <div className="form-section">
            <h4>软件状态</h4>

            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={isEnabled}
                onChange={(e) => setIsEnabled(e.target.checked)}
              />
              软件启用
            </label>

            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={allowDownload}
                onChange={(e) => setAllowDownload(e.target.checked)}
              />
              允许客户下载
            </label>
          </div>

          {/* ------------------
              软件描述
             ------------------ */}
          <div className="form-section">
            <h4>软件描述</h4>

            <textarea
              className="remark-input"
              placeholder="请输入软件功能说明"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>

          {/* ------------------
              备注
             ------------------ */}
          <div className="form-section">
            <h4>备注</h4>

            <textarea
              className="remark-input"
              placeholder="请输入备注"
              value={remark}
              onChange={(e) => setRemark(e.target.value)}
            />
          </div>

          {/* ------------------
              保存 / 取消
             ------------------ */}
          <div className="form-buttons">
            <button className="primary-button" onClick={saveSoftware}>
              {editingSoftwareId === null ? "保存" : "保存修改"}
            </button>

            <button
              className="normal-button"
              onClick={() => {
                setShowSoftwareForm(false);

                setEditingSoftwareId(null);

                clearSoftwareForm();
              }}
            >
              取消
            </button>
          </div>
        </div>
      )}

      {/* ======================
          搜索栏
         ====================== */}
      <SearchBar
        value={searchKeyword}
        placeholder="搜索软件名称、编码、简称、分类或负责人"
        onChange={setSearchKeyword}
        onClear={() => setSearchKeyword("")}
      />

      {/* ======================
          软件列表
         ====================== */}
      <div className="software-table">
        {/* 表头 */}
        <div className="software-table-header">
          <div>软件编码</div>
          <div>软件名称</div>
          <div>分类</div>
          <div>运行平台</div>
          <div>开发负责人</div>
          <div>售后负责人</div>
          <div>状态</div>
          <div>下载</div>
          <div>操作</div>
        </div>

        {/* 没有符合条件的数据 */}
        {filteredSoftwares.length === 0 ? (
          <div className="empty">暂无符合条件的软件</div>
        ) : (
          /**
           * map：
           * 把每一个 Software 转换成一行页面内容。
           */
          filteredSoftwares.map((software) => (
            <div className="software-table-row" key={software.id}>
              {/* 软件编码 */}
              <div>{software.code || "-"}</div>

              {/* 软件名称 */}
              <div>{software.name || "-"}</div>

              {/* 分类 */}
              <div>{software.category || "-"}</div>

              {/* 平台 */}
              <div>{software.platform || "-"}</div>

              {/* 开发负责人 */}
              <div>{software.developer || "-"}</div>

              {/* 售后负责人 */}
              <div>{software.supportOwner || "-"}</div>

              {/* 软件启用状态 */}
              <div>
                <StatusBadge enabled={software.isEnabled} />
              </div>

              {/* 是否允许下载 */}
              <div>{software.allowDownload ? "允许" : "禁止"}</div>

              {/* 操作按钮 */}
              <div className="table-actions">
                <button
                  className="edit-button"
                  onClick={() => editSoftware(software)}
                >
                  编辑
                </button>

                <ConfirmDeleteButton
                  message="确定要删除这个软件吗？"
                  onConfirm={() => deleteSoftware(software.id)}
                />
              </div>
            </div>
          ))
        )}
      </div>
    </div>
  );
}

export default SoftwarePage;
