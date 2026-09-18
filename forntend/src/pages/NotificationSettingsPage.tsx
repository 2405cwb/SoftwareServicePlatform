import { useEffect, useState } from "react";
import { BellRing, RefreshCw, Save, ShieldCheck } from "lucide-react";

import { apiFetch } from "../services/api";

/*
 * 单个外部通知渠道。
 */
interface NotificationPolicyChannelItem {
  id: number;

  channel: string;

  isEnabled: boolean;

  mentionRecipient: boolean;

  mentionAll: boolean;

  createdAt: string;

  updatedAt: string;
}

/*
 * 一条业务通知策略。
 */
interface NotificationPolicyItem {
  id: number;

  eventKey: string;

  eventName: string;

  description: string | null;

  isEnabled: boolean;

  inAppEnabled: boolean;

  recipientStrategy: string;

  defaultLevel: string;

  createdAt: string;

  updatedAt: string;

  channels: NotificationPolicyChannelItem[];
}

const recipientOptions = [
  { value: "Support", label: "全部售后" },
  { value: "Assignee", label: "当前处理人" },
  { value: "Customer", label: "所属客户用户" },
  { value: "AssigneeOrSupport", label: "处理人；未分配时售后" },
  { value: "AssigneeAndSupport", label: "处理人 + 售后" },
  { value: "AssigneeSupportAdmin", label: "处理人 + 售后 + 管理员" },
  { value: "CustomerAndAssignee", label: "客户 + 处理人" },
  { value: "VersionAudience", label: "版本实际发布范围" },
];

const levelOptions = [
  { value: "Info", label: "普通" },
  { value: "Warning", label: "警告" },
  { value: "Danger", label: "严重" },
];

function NotificationSettingsPage() {
  const [policies, setPolicies] = useState<NotificationPolicyItem[]>([]);

  const [loading, setLoading] = useState(true);

  const [errorMessage, setErrorMessage] = useState("");

  const [savingEventKey, setSavingEventKey] = useState<string | null>(null);

  async function loadPolicies() {
    try {
      setLoading(true);
      setErrorMessage("");

      const response = await apiFetch("/api/notification-policies");

      if (!response.ok) {
        const text = await response.text();

        throw new Error(text || `加载通知策略失败：${response.status}`);
      }

      const data = (await response.json()) as NotificationPolicyItem[];

      setPolicies(data);
    } catch (error) {
      console.error("加载通知策略失败：", error);

      setErrorMessage(
        error instanceof Error ? error.message : "加载通知策略失败",
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void loadPolicies();
  }, []);

  function updatePolicy(
    eventKey: string,
    field: "isEnabled" | "inAppEnabled" | "recipientStrategy" | "defaultLevel",
    value: boolean | string,
  ) {
    setPolicies((items) =>
      items.map((item) =>
        item.eventKey === eventKey
          ? {
              ...item,
              [field]: value,
            }
          : item,
      ),
    );
  }

  /*
   * ==========================================
   * 修改某个外部通知渠道
   * ==========================================
   *
   * 特别处理：
   *
   * @接收人
   * 和
   * @所有人
   *
   * 不允许同时开启。
   *
   * 否则一个策略可能出现：
   *
   * MentionRecipient = true
   * MentionAll       = true
   *
   * 语义就会变得混乱。
   */
  function updateChannel(
    eventKey: string,
    channelName: string,
    field: "isEnabled" | "mentionRecipient" | "mentionAll",
    value: boolean,
  ) {
    setPolicies((items) =>
      items.map((item) => {
        if (item.eventKey !== eventKey) {
          return item;
        }

        return {
          ...item,

          channels: item.channels.map((channel) => {
            if (channel.channel !== channelName) {
              return channel;
            }

            /*
             * 开启 @所有人时，
             * 自动关闭 @接收人。
             */
            if (field === "mentionAll" && value) {
              return {
                ...channel,

                mentionAll: true,

                mentionRecipient: false,
              };
            }

            /*
             * 开启 @接收人时，
             * 自动关闭 @所有人。
             */
            if (field === "mentionRecipient" && value) {
              return {
                ...channel,

                mentionRecipient: true,

                mentionAll: false,
              };
            }

            return {
              ...channel,

              [field]: value,
            };
          }),
        };
      }),
    );
  }

  async function savePolicy(policy: NotificationPolicyItem) {
    try {
      setSavingEventKey(policy.eventKey);

      const response = await apiFetch(
        `/api/notification-policies/${encodeURIComponent(policy.eventKey)}`,
        {
          method: "PUT",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            isEnabled: policy.isEnabled,

            inAppEnabled: policy.inAppEnabled,

            recipientStrategy: policy.recipientStrategy,

            defaultLevel: policy.defaultLevel,

            channels: policy.channels.map((channel) => ({
              channel: channel.channel,

              isEnabled: channel.isEnabled,

              mentionRecipient: channel.mentionRecipient,

              mentionAll: channel.mentionAll,
            })),
          }),
        },
      );

      if (!response.ok) {
        const text = await response.text();

        throw new Error(text || `保存失败：${response.status}`);
      }

      await loadPolicies();
    } catch (error) {
      console.error("保存通知策略失败：", error);

      alert(error instanceof Error ? error.message : "保存通知策略失败");
    } finally {
      setSavingEventKey(null);
    }
  }

  /*
   * ==========================================
   * 接收人策略 → 人类可读说明
   * ==========================================
   *
   * 数据库保存的是稳定的技术编码：
   *
   * AssigneeAndSupport
   *
   * 管理员页面不应该要求用户
   * 自己理解这些英文枚举。
   */
  function getRecipientDescription(strategy: string) {
    switch (strategy) {
      case "Support":
        return "全部启用的售后人员";

      case "Assignee":
        return "当前工单处理人";

      case "Customer":
        return "工单所属客户的用户";

      case "AssigneeOrSupport":
        return "已有处理人时通知处理人；未分配时通知全部售后";

      case "AssigneeAndSupport":
        return "当前处理人 + 全部售后人员";

      case "AssigneeSupportAdmin":
        return "当前处理人 + 全部售后 + 管理员";

      case "CustomerAndAssignee":
        return "客户用户 + 当前处理人";

      case "VersionAudience":
        return "本次软件版本实际发布范围内的客户用户";

      default:
        return strategy;
    }
  } /*
   * ==========================================
   * 生成一条策略的最终效果说明
   * ==========================================
   *
   * 这里只负责页面展示，
   * 不参与真正的通知业务逻辑。
   *
   * 真正是否发送仍然以后端
   * NotificationPolicy 为准。
   */
  function getPolicyEffect(policy: NotificationPolicyItem) {
    /*
     * 整个事件关闭时，
     * 下面所有渠道都不会执行。
     */
    if (!policy.isEnabled) {
      return "该事件当前完全关闭，不产生任何通知。";
    }

    const effects: string[] = [];

    /*
     * 站内通知。
     */
    if (policy.inAppEnabled) {
      effects.push("站内通知");
    }

    /*
     * 外部渠道。
     */
    policy.channels.forEach((channel) => {
      if (!channel.isEnabled) {
        return;
      }

      let channelText = getChannelName(channel.channel);

      if (channel.mentionAll) {
        channelText += "（@所有人）";
      } else if (channel.mentionRecipient) {
        channelText += "（@接收人）";
      }

      effects.push(channelText);
    });

    /*
     * 理论上可能出现：
     *
     * Event 开启
     * 站内关闭
     * 所有外部渠道也关闭
     *
     * 这时候实际上没有任何消息会发送。
     */
    if (effects.length === 0) {
      return "事件已启用，但当前没有开启任何通知渠道。";
    }

    return `通知对象：${getRecipientDescription(
      policy.recipientStrategy,
    )}；发送方式：${effects.join(" + ")}。`;
  }
  function getChannelName(channel: string) {
    switch (channel) {
      case "DingTalk":
        return "钉钉";

      case "WeCom":
        return "企业微信";

      case "Feishu":
        return "飞书";

      case "Email":
        return "邮件";

      default:
        return channel;
    }
  }

  function formatDateTime(value: string) {
    return new Date(value).toLocaleString("zh-CN");
  }

  return (
    <div className="content notification-settings-page">
      <div className="notification-settings-header">
        <div>
          <div className="notification-settings-title">
            <BellRing size={25} />
            通知策略
          </div>

          <div className="notification-settings-subtitle">
            集中管理业务事件的站内通知、接收人和外部通知渠道
          </div>
        </div>

        <button
          type="button"
          className="secondary-button notification-refresh-button"
          disabled={loading}
          onClick={() => void loadPolicies()}
        >
          <RefreshCw size={15} />
          刷新
        </button>
      </div>

      <div className="notification-policy-info">
        <ShieldCheck size={21} />

        <div>
          <strong>业务代码只报告“发生了什么”</strong>

          <span>
            是否通知、通知谁、是否发钉钉以及是否 @ 接收人， 都由这里的策略控制。
          </span>
        </div>
      </div>

      {errorMessage && (
        <div className="download-record-error">{errorMessage}</div>
      )}

      {loading ? (
        <div className="dashboard-empty">正在加载通知策略...</div>
      ) : (
        <div className="notification-policy-list">
          {policies.map((policy) => (
            <section
              key={policy.eventKey}
              className={
                "notification-policy-card " +
                (policy.isEnabled ? "" : "is-disabled")
              }
            >
              <div className="notification-policy-card-header">
                <div>
                  <div className="notification-policy-name-row">
                    <strong>{policy.eventName}</strong>

                    <code>{policy.eventKey}</code>
                  </div>

                  <p>{policy.description || "暂无说明"}</p>
                </div>

                <label className="notification-main-switch">
                  <input
                    type="checkbox"
                    checked={policy.isEnabled}
                    onChange={(event) =>
                      updatePolicy(
                        policy.eventKey,
                        "isEnabled",
                        event.target.checked,
                      )
                    }
                  />

                  <span>{policy.isEnabled ? "事件已启用" : "事件已关闭"}</span>
                </label>
              </div>

              <div className="notification-policy-grid">
                <div className="notification-policy-field">
                  <label>站内通知</label>

                  <label className="notification-toggle-line">
                    <input
                      type="checkbox"
                      checked={policy.inAppEnabled}
                      disabled={!policy.isEnabled}
                      onChange={(event) =>
                        updatePolicy(
                          policy.eventKey,
                          "inAppEnabled",
                          event.target.checked,
                        )
                      }
                    />

                    <span>{policy.inAppEnabled ? "开启" : "关闭"}</span>
                  </label>
                </div>

                <div className="notification-policy-field">
                  <label>接收人策略</label>

                  <select
                    value={policy.recipientStrategy}
                    disabled={!policy.isEnabled}
                    onChange={(event) =>
                      updatePolicy(
                        policy.eventKey,
                        "recipientStrategy",
                        event.target.value,
                      )
                    }
                  >
                    {recipientOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                  <div className="notification-recipient-description">
                    {getRecipientDescription(policy.recipientStrategy)}
                  </div>
                </div>

                <div className="notification-policy-field">
                  <label>默认级别</label>

                  <select
                    value={policy.defaultLevel}
                    disabled={!policy.isEnabled}
                    onChange={(event) =>
                      updatePolicy(
                        policy.eventKey,
                        "defaultLevel",
                        event.target.value,
                      )
                    }
                  >
                    {levelOptions.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>
              </div>
              {/* ==========================================
    当前策略实际效果
    ========================================== */}
              <div
                className={
                  "notification-policy-effect " +
                  (policy.isEnabled ? "" : "is-disabled")
                }
              >
                <strong>当前效果</strong>

                <span>{getPolicyEffect(policy)}</span>
              </div>
              <div className="notification-channel-section">
                <div className="notification-channel-title">外部通知渠道</div>

                {policy.channels.length === 0 ? (
                  <div className="notification-channel-empty">
                    当前事件未配置外部通知渠道
                  </div>
                ) : (
                  policy.channels.map((channel) => (
                    <div key={channel.id} className="notification-channel-row">
                      <div className="notification-channel-name">
                        <strong>{getChannelName(channel.channel)}</strong>

                        <span>{channel.channel}</span>
                      </div>

                      <label>
                        <input
                          type="checkbox"
                          checked={channel.isEnabled}
                          disabled={!policy.isEnabled}
                          onChange={(event) =>
                            updateChannel(
                              policy.eventKey,
                              channel.channel,
                              "isEnabled",
                              event.target.checked,
                            )
                          }
                        />
                        发送
                      </label>

                      <label>
                        <input
                          type="checkbox"
                          checked={channel.mentionRecipient}
                          disabled={!policy.isEnabled || !channel.isEnabled}
                          onChange={(event) =>
                            updateChannel(
                              policy.eventKey,
                              channel.channel,
                              "mentionRecipient",
                              event.target.checked,
                            )
                          }
                        />
                        @ 接收人
                      </label>

                      <label>
                        <input
                          type="checkbox"
                          checked={channel.mentionAll}
                          disabled={!policy.isEnabled || !channel.isEnabled}
                          onChange={(event) =>
                            updateChannel(
                              policy.eventKey,
                              channel.channel,
                              "mentionAll",
                              event.target.checked,
                            )
                          }
                        />
                        @ 所有人
                      </label>
                    </div>
                  ))
                )}
              </div>

              <div className="notification-policy-footer">
                <span>
                  最后修改：
                  {formatDateTime(policy.updatedAt)}
                </span>

                <button
                  type="button"
                  className="primary-button notification-save-button"
                  disabled={savingEventKey === policy.eventKey}
                  onClick={() => void savePolicy(policy)}
                >
                  <Save size={15} />

                  {savingEventKey === policy.eventKey ? "保存中" : "保存策略"}
                </button>
              </div>
            </section>
          ))}
        </div>
      )}
    </div>
  );
}

export default NotificationSettingsPage;
