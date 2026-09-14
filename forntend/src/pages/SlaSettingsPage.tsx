import { useEffect, useState } from "react";

import { Clock3, Save, ShieldCheck } from "lucide-react";

import { apiFetch } from "../services/api";

/*
 * 后端 GET /api/ticket-sla-rules
 * 返回的数据结构。
 */
interface TicketSlaRuleItem {
  configured: boolean;

  priority: string;

  firstResponseTargetMinutes: number | null;

  resolutionTargetMinutes: number | null;

  isEnabled: boolean;

  updatedAt: string | null;
  warningBeforeMinutes: number | null;
}

/*
 * 页面编辑状态。
 *
 * 输入框使用 string，
 * 是因为用户删除内容时必须允许暂时为空。
 */
interface EditableSlaRule {
  configured: boolean;

  priority: string;

  firstResponseTargetMinutes: string;

  resolutionTargetMinutes: string;

  isEnabled: boolean;

  updatedAt: string | null;
  warningBeforeMinutes: string;
}

function SlaSettingsPage() {
  const [rules, setRules] = useState<EditableSlaRule[]>([]);

  const [loading, setLoading] = useState(true);

  const [errorMessage, setErrorMessage] = useState("");

  /*
   * 当前正在保存哪个优先级。
   *
   * null：
   * 没有保存操作。
   *
   * High：
   * 正在保存 High。
   */
  const [savingPriority, setSavingPriority] = useState<string | null>(null);

  /*
   * ==========================================
   * 查询 SLA 规则
   * ==========================================
   */
  async function loadRules() {
    try {
      setLoading(true);

      setErrorMessage("");

      const response = await apiFetch("/api/ticket-sla-rules");

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `加载 SLA 规则失败：${response.status}`);
      }

      const data = (await response.json()) as TicketSlaRuleItem[];

      /*
       * 后端 number/null
       *
       * 转成前端 input 可以直接使用的字符串。
       */
      const editableRules = data.map((item) => ({
        configured: item.configured,

        priority: item.priority,

        firstResponseTargetMinutes:
          item.firstResponseTargetMinutes?.toString() ?? "",

        resolutionTargetMinutes: item.resolutionTargetMinutes?.toString() ?? "",

        isEnabled: item.isEnabled,

        updatedAt: item.updatedAt,

        warningBeforeMinutes: item.warningBeforeMinutes?.toString() ?? "",
      }));

      setRules(editableRules);
    } catch (error) {
      console.error("加载 SLA 规则失败：", error);

      setErrorMessage("加载 SLA 规则失败，请稍后重试。");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadRules();
  }, []);

  /*
   * ==========================================
   * 修改某一行的字段
   * ==========================================
   */
  function updateRule(
    priority: string,
    field:
      | "firstResponseTargetMinutes"
      | "resolutionTargetMinutes"
      | "warningBeforeMinutes"
      | "isEnabled",
    value: string | boolean,
  ) {
    setRules((currentRules) =>
      currentRules.map((rule) =>
        rule.priority === priority
          ? {
              ...rule,

              [field]: value,
            }
          : rule,
      ),
    );
  }

  /*
   * ==========================================
   * 保存单条 SLA
   * ==========================================
   */
  async function saveRule(rule: EditableSlaRule) {
    /*
     * string -> number
     */
    const firstResponseMinutes = Number(rule.firstResponseTargetMinutes);

    const resolutionMinutes = Number(rule.resolutionTargetMinutes);
    const warningBeforeMinutes = rule.warningBeforeMinutes
      ? Number(rule.warningBeforeMinutes)
      : 0;
    /*
     * 前端基础校验。
     *
     * 真正业务安全仍以后端为准。
     */
    if (
      !rule.firstResponseTargetMinutes ||
      !Number.isFinite(firstResponseMinutes) ||
      firstResponseMinutes <= 0
    ) {
      alert("请输入正确的首次响应目标分钟数");

      return;
    }

    if (
      !rule.resolutionTargetMinutes ||
      !Number.isFinite(resolutionMinutes) ||
      resolutionMinutes <= 0
    ) {
      alert("请输入正确的解决目标分钟数");

      return;
    }

    if (resolutionMinutes < firstResponseMinutes) {
      alert("解决目标不能小于首次响应目标");

      return;
    }

    if (!Number.isFinite(warningBeforeMinutes) || warningBeforeMinutes < 0) {
      alert("请输入正确的预警分钟数");

      return;
    }
    if (warningBeforeMinutes > resolutionMinutes) {
      alert("预警时间不能大于解决目标时间");

      return;
    }
    try {
      setSavingPriority(rule.priority);

      const response = await apiFetch(
        `/api/ticket-sla-rules/${rule.priority}`,
        {
          method: "PUT",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            firstResponseTargetMinutes: firstResponseMinutes,

            resolutionTargetMinutes: resolutionMinutes,

            isEnabled: rule.isEnabled,
          }),
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `保存失败：${response.status}`);
      }

      /*
       * 保存成功后重新读取后端数据。
       *
       * 这样：
       *
       * configured
       * updatedAt
       *
       * 都能同步成服务器真实状态。
       */
      await loadRules();
    } catch (error) {
      console.error("保存 SLA 规则失败：", error);

      alert(error instanceof Error ? error.message : "保存 SLA 规则失败");
    } finally {
      setSavingPriority(null);
    }
  }

  /*
   * ==========================================
   * 优先级中文名称
   * ==========================================
   */
  function getPriorityName(priority: string) {
    switch (priority) {
      case "Low":
        return "低";

      case "Normal":
        return "普通";

      case "High":
        return "高";

      case "Urgent":
        return "紧急";

      default:
        return priority;
    }
  }

  /*
   * ==========================================
   * 分钟转换成人类容易理解的时间
   * ==========================================
   *
   * 30
   * → 30分钟
   *
   * 120
   * → 2小时
   *
   * 1440
   * → 1天
   *
   * 2880
   * → 2天
   */
  function formatMinutes(value: string) {
    if (!value) {
      return "未设置";
    }

    const minutes = Number(value);

    if (!Number.isFinite(minutes) || minutes <= 0) {
      return "无效时间";
    }

    if (minutes < 60) {
      return `${minutes} 分钟`;
    }

    if (minutes % 1440 === 0) {
      return `${minutes / 1440} 天`;
    }

    if (minutes % 60 === 0) {
      return `${minutes / 60} 小时`;
    }

    if (minutes >= 1440) {
      return `${(minutes / 1440).toFixed(1)} 天`;
    }

    return `${(minutes / 60).toFixed(1)} 小时`;
  }

  function formatDateTime(value: string | null) {
    if (!value) {
      return "尚未配置";
    }

    return new Date(value).toLocaleString("zh-CN");
  }

  return (
    <div className="content sla-settings-page">
      {/* ======================================
          页面标题
          ====================================== */}
      <div className="sla-settings-header">
        <div>
          <div className="sla-settings-title">
            <ShieldCheck size={25} />
            SLA 设置
          </div>

          <div className="sla-settings-subtitle">
            配置不同优先级工单的首次响应和解决时间目标
          </div>
        </div>
      </div>

      {/* ======================================
          SLA 说明
          ====================================== */}
      <div className="sla-info-card">
        <Clock3 size={21} />

        <div>
          <div className="sla-info-title">SLA 时间从工单创建时间开始计算</div>

          <div className="sla-info-description">
            首次响应： CreatedAt → FirstResponseAt； 解决时间： CreatedAt →
            ResolvedAt。 未启用的规则不会参与 SLA 统计。
          </div>
        </div>
      </div>

      {errorMessage && (
        <div className="download-record-error">{errorMessage}</div>
      )}

      {/* ======================================
          SLA 表格
          ====================================== */}
      <div className="sla-settings-card">
        {loading ? (
          <div className="dashboard-empty">正在加载 SLA 配置...</div>
        ) : (
          <div className="sla-table-wrapper">
            <table className="sla-table">
              <thead>
                <tr>
                  <th>优先级</th>

                  <th>首次响应目标</th>

                  <th>解决目标</th>
                  <th>提前预警</th>
                  <th>状态</th>

                  <th>最后修改</th>

                  <th>操作</th>
                </tr>
              </thead>

              <tbody>
                {rules.map((rule) => (
                  <tr key={rule.priority}>
                    {/* =========================
                          优先级
                          ========================= */}
                    <td>
                      <div className="sla-priority-cell">
                        <span
                          className={
                            "sla-priority-badge " +
                            `sla-priority-${rule.priority.toLowerCase()}`
                          }
                        >
                          {getPriorityName(rule.priority)}
                        </span>

                        <span className="sla-priority-code">
                          {rule.priority}
                        </span>
                      </div>
                    </td>

                    {/* =========================
                          首次响应
                          ========================= */}
                    <td>
                      <div className="sla-time-editor">
                        <div className="sla-input-row">
                          <input
                            type="number"
                            min="1"
                            value={rule.firstResponseTargetMinutes}
                            placeholder="例如 120"
                            onChange={(event) =>
                              updateRule(
                                rule.priority,

                                "firstResponseTargetMinutes",

                                event.target.value,
                              )
                            }
                          />

                          <span>分钟</span>
                        </div>

                        <div className="sla-time-preview">
                          {formatMinutes(rule.firstResponseTargetMinutes)}
                        </div>
                      </div>
                    </td>
                    <td>
                      <div className="sla-time-editor">
                        <div className="sla-input-row">
                          <input
                            type="number"
                            min="0"
                            value={rule.warningBeforeMinutes}
                            placeholder="例如 120"
                            onChange={(event) =>
                              updateRule(
                                rule.priority,

                                "warningBeforeMinutes",

                                event.target.value,
                              )
                            }
                          />

                          <span>分钟</span>
                        </div>

                        <div className="sla-time-preview">
                          {rule.warningBeforeMinutes === "" ||
                          Number(rule.warningBeforeMinutes) === 0
                            ? "不预警"
                            : `提前 ${formatMinutes(
                                rule.warningBeforeMinutes,
                              )}`}
                        </div>
                      </div>
                    </td>
                    {/* =========================
                          解决目标
                          ========================= */}
                    <td>
                      <div className="sla-time-editor">
                        <div className="sla-input-row">
                          <input
                            type="number"
                            min="1"
                            value={rule.resolutionTargetMinutes}
                            placeholder="例如 1440"
                            onChange={(event) =>
                              updateRule(
                                rule.priority,

                                "resolutionTargetMinutes",

                                event.target.value,
                              )
                            }
                          />

                          <span>分钟</span>
                        </div>

                        <div className="sla-time-preview">
                          {formatMinutes(rule.resolutionTargetMinutes)}
                        </div>
                      </div>
                    </td>

                    {/* =========================
                          启用状态
                          ========================= */}
                    <td>
                      <label className="sla-enable-switch">
                        <input
                          type="checkbox"
                          checked={rule.isEnabled}
                          onChange={(event) =>
                            updateRule(
                              rule.priority,

                              "isEnabled",

                              event.target.checked,
                            )
                          }
                        />

                        <span
                          className={
                            rule.isEnabled
                              ? "sla-enabled-text"
                              : "sla-disabled-text"
                          }
                        >
                          {rule.isEnabled ? "已启用" : "未启用"}
                        </span>
                      </label>
                    </td>

                    {/* =========================
                          更新时间
                          ========================= */}
                    <td>
                      <div className="sla-update-time">
                        {formatDateTime(rule.updatedAt)}
                      </div>

                      {!rule.configured && (
                        <div className="sla-not-configured">尚未保存</div>
                      )}
                    </td>

                    {/* =========================
                          保存
                          ========================= */}
                    <td>
                      <button
                        type="button"
                        className="primary-button sla-save-button"
                        disabled={savingPriority === rule.priority}
                        onClick={() => saveRule(rule)}
                      >
                        <Save size={15} />

                        {savingPriority === rule.priority ? "保存中" : "保存"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

export default SlaSettingsPage;
