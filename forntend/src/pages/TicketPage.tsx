import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useLocation, useSearchParams } from "react-router-dom";
import {
  AlertTriangle,
  CheckCircle2,
  Download,
  FileText,
  Filter,
  Inbox,
  MessageSquare,
  Paperclip,
  Plus,
  RefreshCw,
  RotateCcw,
  Search,
  Send,
  ShieldAlert,
  UserRoundCheck,
  X,
} from "lucide-react";
import { apiFetch } from "../services/api";
import { getSessionUser } from "../utils/session";
import {
  formatDateTime,
  formatFileSize,
  getPriorityName,
  getSourceName,
  getStatusName,
} from "../utils/format";
import {
  PriorityBadge,
  StatusBadge,
} from "../components/dashboard/StatusBadge";

interface TicketItem {
  id: number;
  ticketNo: string;
  title: string;
  description: string;
  status: string;
  priority: string;
  customerId: number;
  customerName: string;
  softwareId: number;
  softwareName: string;
  createdByUserId: number;
  createdByName: string;
  createdByUsername: string;
  assignedToUserId: number | null;
  assignedToName: string | null;
  source: string;
  createdAt: string;
  updatedAt: string;
  firstResponseAt: string | null;
  resolvedAt: string | null;
  closedAt: string | null;
}

interface TicketRecordItem {
  id: number;
  ticketId: number;
  recordType: string;
  content: string;
  isInternal: boolean;
  createdByUserId: number;
  createdByName: string;
  createdByRole: string;
  createdAt: string;
}

interface TicketAttachmentItem {
  id: number;
  ticketId: number;
  ticketRecordId: number | null;
  fileName: string;
  fileSize: number;
  contentType: string;
  isInternal: boolean;
  uploadedByUserId: number;
  uploadedByName: string;
  uploadedByRole: string;
  createdAt: string;
}

interface TicketMeta {
  id: number;
  ticketNo: string;
  priority: string;
  source: string;
  status: string;
  slaPriority: string | null;
  slaFirstResponseTargetMinutes: number | null;
  slaResolutionTargetMinutes: number | null;
  slaAppliedAt: string | null;
  responseDeadline: string | null;
  resolutionDeadline: string | null;
  responseOverdue: boolean;
  resolutionOverdue: boolean;
  firstResponseAt: string | null;
  resolvedAt: string | null;
  closedAt: string | null;
  customerName: string;
  softwareName: string;
  assignedToName: string | null;
  canTriage: boolean;
}

interface IntakeOptions {
  customers: Array<{
    id: number;
    name: string;
    code: string;
    softwares: Array<{ id: number; name: string; code: string }>;
  }>;
  assignees: Array<{
    id: number;
    displayName: string;
    username: string;
    role: string;
  }>;
  priorities: string[];
  sources: string[];
}

interface MySoftwareOption {
  softwareId: number;
  softwareName: string;
  softwareCode: string;
}

type ComposerMode = "public" | "internal";

function TicketPage() {
  const user = getSessionUser();
  const [searchParams, setSearchParams] = useSearchParams();
  /*
   * 当前路由信息。
   *
   * 除了 pathname / search 之外，
   * React Router 的 location 还可以携带 state。
   *
   * 我们会利用它接收：
   *
   * MainLayout 点击通知时发送的刷新标记。
   */
  const location = useLocation();

  /*
   * ==========================================
   * 通知刷新标记
   * ==========================================
   *
   * 普通菜单进入工单页面：
   *
   * notificationRefreshKey = undefined
   *
   *
   * 从小铃铛点击通知进入：
   *
   * notificationRefreshKey = 当前时间戳
   *
   *
   * 每次点击通知时间戳都会变化，
   * 所以后面的 useEffect 就可以知道：
   *
   * “用户又点击了一条通知，需要重新获取服务器数据。”
   */
  const notificationRefreshKey = (
    location.state as {
      notificationRefreshKey?: number;
    } | null
  )?.notificationRefreshKey;
  const role = user?.role ?? "";
  const isCustomer = role === "Customer";
  const isDeveloper = role === "Developer";
  const canTriage = role === "Admin" || role === "Support";
  const canResolve =
    role === "Admin" || role === "Support" || role === "Developer";
  const canClose =
    role === "Admin" || role === "Support" || role === "Customer";

  const [tickets, setTickets] = useState<TicketItem[]>([]);
  const [selectedTicket, setSelectedTicket] = useState<TicketItem | null>(null);
  const [records, setRecords] = useState<TicketRecordItem[]>([]);
  const [attachments, setAttachments] = useState<TicketAttachmentItem[]>([]);
  const [meta, setMeta] = useState<TicketMeta | null>(null);
  const [intakeOptions, setIntakeOptions] = useState<IntakeOptions | null>(
    null,
  );
  const [mySoftware, setMySoftware] = useState<MySoftwareOption[]>([]);

  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [error, setError] = useState("");
  const [keyword, setKeyword] = useState("");
  const [statusFilter, setStatusFilter] = useState(
    searchParams.get("status") ?? "",
  );
  const [priorityFilter, setPriorityFilter] = useState(
    searchParams.get("priority") ?? "",
  );

  const [composerMode, setComposerMode] = useState<ComposerMode>("public");
  const [replyText, setReplyText] = useState("");
  const [replyFiles, setReplyFiles] = useState<File[]>([]);
  const [sending, setSending] = useState(false);

  const [showNewTicket, setShowNewTicket] = useState(
    searchParams.get("new") === "1",
  );
  const [showStaffCreate, setShowStaffCreate] = useState(false);
  const [creating, setCreating] = useState(false);

  const [customerSoftwareId, setCustomerSoftwareId] = useState("");
  const [newTitle, setNewTitle] = useState("");
  const [newDescription, setNewDescription] = useState("");
  const [newFiles, setNewFiles] = useState<File[]>([]);

  const [staffCustomerId, setStaffCustomerId] = useState("");
  const [staffSoftwareId, setStaffSoftwareId] = useState("");
  const [staffTitle, setStaffTitle] = useState("");
  const [staffDescription, setStaffDescription] = useState("");
  const [staffPriority, setStaffPriority] = useState("Normal");
  const [staffSource, setStaffSource] = useState("WeChat");
  const [staffAssigneeId, setStaffAssigneeId] = useState("");
  const [staffFiles, setStaffFiles] = useState<File[]>([]);

  const [triagePriority, setTriagePriority] = useState("Normal");
  const [triageAssigneeId, setTriageAssigneeId] = useState("");
  const [triaging, setTriaging] = useState(false);

  async function loadTickets(openId?: number) {
    try {
      setLoading(true);
      setError("");

      const response = await apiFetch("/api/tickets");
      if (!response.ok) {
        throw new Error(await response.text());
      }

      const data = (await response.json()) as TicketItem[];
      setTickets(data);

      const targetId = openId ?? Number(searchParams.get("ticketId") || 0);
      if (targetId > 0) {
        const target = data.find((x) => x.id === targetId);
        if (target) {
          await openTicket(target, false);
        }
      } else if (selectedTicket) {
        const refreshed = data.find((x) => x.id === selectedTicket.id) ?? null;
        setSelectedTicket(refreshed);
      }
    } catch (e) {
      console.error(e);
      setError("加载工单失败");
    } finally {
      setLoading(false);
    }
  }

  async function loadIntakeOptions() {
    if (!canTriage) return;
    try {
      const response = await apiFetch("/api/ticket-workflow/intake-options");
      if (response.ok) {
        setIntakeOptions((await response.json()) as IntakeOptions);
      }
    } catch (e) {
      console.error("加载分诊选项失败：", e);
    }
  }

  async function loadMySoftware() {
    if (!isCustomer) return;
    try {
      const response = await apiFetch("/api/my-software");
      if (response.ok) {
        const data = (await response.json()) as MySoftwareOption[];
        setMySoftware(data);
        if (data.length === 1) {
          setCustomerSoftwareId(String(data[0].softwareId));
        }
      }
    } catch (e) {
      console.error("加载我的软件失败：", e);
    }
  }

  useEffect(() => {
    loadTickets();
    loadIntakeOptions();
    loadMySoftware();
  }, []);
  /*
   * ==========================================
   * 从通知进入工单页面时重新加载工单
   * ==========================================
   *
   * 为什么需要这个？
   *
   * 假设售后当前已经停留在工单页面：
   *
   * tickets = [
   *     工单1,
   *     工单2
   * ]
   *
   * 此时客户创建了工单3。
   *
   * 虽然 SignalR 已经告诉浏览器：
   *
   * “有新通知”
   *
   * 但 tickets 数组不会自动出现工单3。
   *
   *
   * 所以点击通知以后，
   * 必须重新执行：
   *
   * GET /api/tickets
   *
   * 获取服务器最新数据。
   *
   *
   * 对 Developer 也一样：
   *
   * 某张工单刚刚被分配给 Developer，
   * 原来的 tickets 中可能根本没有这张工单，
   * 必须重新请求服务器。
   */
  useEffect(() => {
    /*
     * 没有刷新标记，
     * 说明不是通过通知点击触发的，
     * 不需要额外重新加载。
     */
    if (!notificationRefreshKey) {
      return;
    }

    /*
     * 通知 TargetUrl 通常类似：
     *
     * /tickets?ticketId=35
     *
     * 这里读取需要自动打开的工单 ID。
     */
    const ticketId = Number(searchParams.get("ticketId") || 0);

    /*
     * loadTickets(openId)
     *
     * 会：
     *
     * 1. 重新 GET /api/tickets
     * 2. 更新 tickets[]
     * 3. 找到指定 ticketId
     * 4. 自动打开工单详情
     *
     * 所以一次就可以同时解决：
     *
     * 列表不刷新
     * +
     * 详情不刷新
     */
    void loadTickets(ticketId > 0 ? ticketId : undefined);
  }, [notificationRefreshKey]);
  useEffect(() => {
    setStatusFilter(searchParams.get("status") ?? "");
    setPriorityFilter(searchParams.get("priority") ?? "");
    if (searchParams.get("new") === "1" && isCustomer) {
      setShowNewTicket(true);
    }
  }, [searchParams, isCustomer]);

  // 支持 Dashboard 跳转、浏览器前进/后退时自动定位到 URL 指定工单。
  useEffect(() => {
    const ticketId = Number(searchParams.get("ticketId") || 0);
    if (
      ticketId <= 0 ||
      selectedTicket?.id === ticketId ||
      tickets.length === 0
    ) {
      return;
    }

    const target = tickets.find((ticket) => ticket.id === ticketId);
    if (target) {
      openTicket(target, false);
    }
  }, [searchParams, tickets, selectedTicket?.id]);

  async function openTicket(ticket: TicketItem, updateUrl = true) {
    setSelectedTicket(ticket);
    setDetailLoading(true);
    setRecords([]);
    setAttachments([]);
    setMeta(null);
    setReplyText("");
    setReplyFiles([]);
    setComposerMode("public");

    if (updateUrl) {
      const next = new URLSearchParams(searchParams);
      next.set("ticketId", String(ticket.id));
      setSearchParams(next, { replace: true });
    }

    try {
      const requests: Promise<Response>[] = [
        apiFetch(`/api/tickets/${ticket.id}/records`),
        apiFetch(`/api/tickets/${ticket.id}/attachments`),
      ];

      if (!isCustomer) {
        requests.push(apiFetch(`/api/ticket-workflow/${ticket.id}/meta`));
      }

      const responses = await Promise.all(requests);
      if (!responses[0].ok || !responses[1].ok) {
        throw new Error("加载工单详情失败");
      }

      setRecords((await responses[0].json()) as TicketRecordItem[]);
      setAttachments((await responses[1].json()) as TicketAttachmentItem[]);

      if (!isCustomer && responses[2]?.ok) {
        const metaData = (await responses[2].json()) as TicketMeta;
        setMeta(metaData);
        setTriagePriority(
          metaData.priority === "Unclassified" ? "Normal" : metaData.priority,
        );
        setTriageAssigneeId(
          ticket.assignedToUserId ? String(ticket.assignedToUserId) : "",
        );
      }
    } catch (e) {
      console.error(e);
      alert("加载工单详情失败");
    } finally {
      setDetailLoading(false);
    }
  }

  const filteredTickets = useMemo(() => {
    const q = keyword.trim().toLowerCase();
    const scope = searchParams.get("scope") ?? "";
    const customerId = Number(searchParams.get("customerId") || 0);
    const softwareId = Number(searchParams.get("softwareId") || 0);
    const period = searchParams.get("period") ?? "";
    const now = new Date();

    return tickets.filter((ticket) => {
      if (
        q &&
        ![
          ticket.ticketNo,
          ticket.title,
          ticket.customerName,
          ticket.softwareName,
          ticket.assignedToName ?? "",
        ].some((x) => x.toLowerCase().includes(q))
      ) {
        return false;
      }

      if (
        statusFilter === "open" &&
        !["Pending", "Processing"].includes(ticket.status)
      )
        return false;
      if (
        statusFilter &&
        statusFilter !== "open" &&
        ticket.status !== statusFilter
      )
        return false;
      if (priorityFilter && ticket.priority !== priorityFilter) return false;
      if (customerId > 0 && ticket.customerId !== customerId) return false;
      if (softwareId > 0 && ticket.softwareId !== softwareId) return false;
      if (
        scope === "my" &&
        user &&
        ticket.assignedToUserId !== user.id &&
        !isCustomer
      )
        return false;
      if (scope === "unassigned" && ticket.assignedToUserId !== null)
        return false;

      if (period === "thisMonth") {
        const created = new Date(ticket.createdAt);
        if (
          created.getFullYear() !== now.getFullYear() ||
          created.getMonth() !== now.getMonth()
        )
          return false;
      }

      return true;
    });
  }, [
    tickets,
    keyword,
    statusFilter,
    priorityFilter,
    searchParams,
    user,
    isCustomer,
  ]);

  function updateFilter(name: string, value: string) {
    const next = new URLSearchParams(searchParams);
    if (value) next.set(name, value);
    else next.delete(name);
    next.delete("ticketId");
    setSearchParams(next, { replace: true });
  }

  async function submitReply() {
    if (!selectedTicket || !replyText.trim()) {
      return;
    }

    try {
      setSending(true);
      const isInternal = composerMode === "internal";
      const response = await apiFetch(
        `/api/tickets/${selectedTicket.id}/records`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ content: replyText.trim(), isInternal }),
        },
      );

      if (!response.ok) {
        throw new Error((await response.text()) || "发送失败");
      }

      const record = await response.json();

      if (replyFiles.length > 0) {
        const form = new FormData();
        replyFiles.forEach((file) => form.append("files", file));
        form.append("isInternal", String(isInternal));
        form.append("ticketRecordId", String(record.id));

        const uploadResponse = await apiFetch(
          `/api/tickets/${selectedTicket.id}/attachments`,
          {
            method: "POST",
            body: form,
          },
        );

        if (!uploadResponse.ok) {
          alert(
            "回复已发送，但部分附件上传失败：" + (await uploadResponse.text()),
          );
        }
      }

      setReplyText("");
      setReplyFiles([]);
      await refreshSelectedTicket();
    } catch (e) {
      alert("发送失败：" + String(e));
    } finally {
      setSending(false);
    }
  }

  async function refreshSelectedTicket() {
    if (!selectedTicket) return;
    const id = selectedTicket.id;
    await loadTickets(id);
  }

  async function triageTicket() {
    if (!selectedTicket) return;

    try {
      setTriaging(true);
      const response = await apiFetch(
        `/api/ticket-workflow/${selectedTicket.id}/triage`,
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            priority: triagePriority,
            assignedToUserId: triageAssigneeId
              ? Number(triageAssigneeId)
              : null,
          }),
        },
      );

      if (!response.ok) {
        throw new Error(await response.text());
      }

      await refreshSelectedTicket();
    } catch (e) {
      alert("分诊失败：" + String(e));
    } finally {
      setTriaging(false);
    }
  }

  async function resolveTicket() {
    if (!selectedTicket) return;
    const content = window.prompt("请输入解决说明：");
    if (!content?.trim()) return;

    const response = await apiFetch(
      `/api/tickets/${selectedTicket.id}/resolve`,
      {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ content: content.trim() }),
      },
    );

    if (!response.ok) {
      alert((await response.text()) || "标记解决失败");
      return;
    }

    await refreshSelectedTicket();
  }

  async function closeTicket() {
    if (!selectedTicket) return;
    if (!window.confirm("确认关闭这个工单吗？")) return;

    const response = await apiFetch(`/api/tickets/${selectedTicket.id}/close`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        content: isCustomer ? "客户确认问题已解决" : "工单处理完成并关闭",
      }),
    });

    if (!response.ok) {
      alert((await response.text()) || "关闭失败");
      return;
    }

    await refreshSelectedTicket();
  }

  async function reopenTicket() {
    if (!selectedTicket) return;
    const content = window.prompt("请输入重新打开的原因：");
    if (!content?.trim()) return;

    const response = await apiFetch(
      `/api/tickets/${selectedTicket.id}/reopen`,
      {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ content: content.trim() }),
      },
    );

    if (!response.ok) {
      alert((await response.text()) || "重新打开失败");
      return;
    }

    await refreshSelectedTicket();
  }

  async function createCustomerTicket() {
    if (!customerSoftwareId || !newTitle.trim() || !newDescription.trim()) {
      alert("请完整填写软件、标题和问题描述");
      return;
    }

    try {
      setCreating(true);
      // Priority 仅用于兼容旧接口的参数校验。
      // 后端 SaveChangesInterceptor 会无条件把 Portal 新工单强制改成 Unclassified，
      // 客户无法通过伪造 HTTP 请求决定真实优先级。
      const response = await apiFetch("/api/tickets", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          softwareId: Number(customerSoftwareId),
          title: newTitle.trim(),
          description: newDescription.trim(),
          priority: "Normal",
        }),
      });

      if (!response.ok) throw new Error(await response.text());
      const result = await response.json();
      await uploadInitialFiles(result.id, newFiles, false);

      resetCustomerCreate();
      setShowNewTicket(false);
      await loadTickets(result.id);
    } catch (e) {
      alert("提交工单失败：" + String(e));
    } finally {
      setCreating(false);
    }
  }

  async function createStaffTicket() {
    if (
      !staffCustomerId ||
      !staffSoftwareId ||
      !staffTitle.trim() ||
      !staffDescription.trim()
    ) {
      alert("请完整填写客户、软件、标题和问题描述");
      return;
    }

    try {
      setCreating(true);
      const response = await apiFetch("/api/ticket-workflow/create", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          customerId: Number(staffCustomerId),
          softwareId: Number(staffSoftwareId),
          title: staffTitle.trim(),
          description: staffDescription.trim(),
          priority: staffPriority,
          source: staffSource,
          assignedToUserId: staffAssigneeId ? Number(staffAssigneeId) : null,
        }),
      });

      if (!response.ok) throw new Error(await response.text());
      const result = await response.json();
      await uploadInitialFiles(result.id, staffFiles, false);
      resetStaffCreate();
      setShowStaffCreate(false);
      await loadTickets(result.id);
    } catch (e) {
      alert("代录工单失败：" + String(e));
    } finally {
      setCreating(false);
    }
  }

  async function uploadInitialFiles(
    ticketId: number,
    files: File[],
    isInternal: boolean,
  ) {
    if (files.length === 0) return;
    const form = new FormData();
    files.forEach((file) => form.append("files", file));
    form.append("isInternal", String(isInternal));
    const response = await apiFetch(`/api/tickets/${ticketId}/attachments`, {
      method: "POST",
      body: form,
    });
    if (!response.ok) {
      alert("工单已创建，但附件上传失败：" + (await response.text()));
    }
  }

  function resetCustomerCreate() {
    setNewTitle("");
    setNewDescription("");
    setNewFiles([]);
    if (mySoftware.length !== 1) setCustomerSoftwareId("");
  }

  function resetStaffCreate() {
    setStaffCustomerId("");
    setStaffSoftwareId("");
    setStaffTitle("");
    setStaffDescription("");
    setStaffPriority("Normal");
    setStaffSource("WeChat");
    setStaffAssigneeId("");
    setStaffFiles([]);
  }

  async function downloadAttachment(attachment: TicketAttachmentItem) {
    const response = await apiFetch(
      `/api/tickets/attachments/${attachment.id}/download`,
    );
    if (!response.ok) {
      alert((await response.text()) || "附件下载失败");
      return;
    }
    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = attachment.fileName;
    link.click();
    URL.revokeObjectURL(url);
  }

  const staffSelectedCustomer = intakeOptions?.customers.find(
    (x) => String(x.id) === staffCustomerId,
  );

  return (
    <div className="content u-page u-ticket-page">
      <header className="u-page-header u-ticket-page-head">
        <div>
          <span className="u-eyebrow">SERVICE DESK</span>
          <h2>
            {isCustomer
              ? "我的服务工单"
              : isDeveloper
                ? "我的处理工单"
                : "工单工作台"}
          </h2>
          <p>
            {isCustomer
              ? "提交问题、补充信息并跟踪处理进展"
              : "受理、分诊、沟通、解决和关闭都集中在一个工作区"}
          </p>
        </div>
        <div className="u-page-actions">
          <button
            type="button"
            className="u-secondary-button"
            onClick={() => loadTickets()}
          >
            <RefreshCw size={16} />
            刷新
          </button>
          {isCustomer && (
            <button
              type="button"
              className="primary-button"
              onClick={() => setShowNewTicket(true)}
            >
              <Plus size={16} />
              提交问题
            </button>
          )}
          {canTriage && (
            <button
              type="button"
              className="primary-button"
              onClick={() => setShowStaffCreate(true)}
            >
              <Plus size={16} />
              代客户建单
            </button>
          )}
        </div>
      </header>

      <div className="u-ticket-toolbar">
        <div className="u-search-box">
          <Search size={17} />
          <input
            value={keyword}
            onChange={(e) => setKeyword(e.target.value)}
            placeholder="搜索工单号、标题、客户、软件、处理人"
          />
        </div>
        <div className="u-filter-group">
          <Filter size={15} />
          <select
            value={statusFilter}
            onChange={(e) => {
              setStatusFilter(e.target.value);
              updateFilter("status", e.target.value);
            }}
          >
            <option value="">全部状态</option>
            <option value="open">未结束</option>
            <option value="Pending">待处理</option>
            <option value="Processing">处理中</option>
            <option value="Resolved">已解决</option>
            <option value="Closed">已关闭</option>
          </select>
          {!isCustomer && (
            <select
              value={priorityFilter}
              onChange={(e) => {
                setPriorityFilter(e.target.value);
                updateFilter("priority", e.target.value);
              }}
            >
              <option value="">全部优先级</option>
              <option value="Unclassified">待分诊</option>
              <option value="Urgent">紧急</option>
              <option value="High">高</option>
              <option value="Normal">普通</option>
              <option value="Low">低</option>
            </select>
          )}
        </div>
        <span className="u-result-count">{filteredTickets.length} 个工单</span>
      </div>

      {error && <div className="u-error-card">{error}</div>}

      <div className="u-ticket-workspace">
        <aside className="u-ticket-inbox">
          {loading ? (
            <div className="u-empty-state">加载中...</div>
          ) : filteredTickets.length === 0 ? (
            <div className="u-empty-state">
              <Inbox size={22} />
              暂无符合条件的工单
            </div>
          ) : (
            filteredTickets.map((ticket) => (
              <button
                key={ticket.id}
                type="button"
                className={`u-ticket-inbox-item ${selectedTicket?.id === ticket.id ? "is-active" : ""}`}
                onClick={() => openTicket(ticket)}
              >
                <div className="u-ticket-inbox-top">
                  <span>{ticket.ticketNo}</span>
                  <span>{formatDateTime(ticket.updatedAt).slice(5, 16)}</span>
                </div>
                <strong>{ticket.title}</strong>
                <div className="u-ticket-inbox-badges">
                  <StatusBadge status={ticket.status} />
                  {!isCustomer && <PriorityBadge priority={ticket.priority} />}
                </div>
                <div className="u-ticket-inbox-meta">
                  {ticket.customerName} · {ticket.softwareName}
                </div>
              </button>
            ))
          )}
        </aside>

        <main className="u-ticket-conversation">
          {!selectedTicket ? (
            <div className="u-ticket-placeholder">
              <MessageSquare size={42} />
              <h3>选择一个工单开始处理</h3>
              <p>左侧选择工单后，这里会显示完整沟通时间线。</p>
            </div>
          ) : detailLoading ? (
            <div className="u-ticket-placeholder">正在加载工单详情...</div>
          ) : (
            <>
              <div className="u-ticket-detail-head">
                <div>
                  <div className="u-ticket-detail-no">
                    {selectedTicket.ticketNo}
                  </div>
                  <h3>{selectedTicket.title}</h3>
                  <div className="u-ticket-head-badges">
                    <StatusBadge status={selectedTicket.status} />
                    {!isCustomer && (
                      <PriorityBadge priority={selectedTicket.priority} />
                    )}
                  </div>
                </div>
                <div className="u-ticket-actions">
                  {canResolve &&
                    ["Pending", "Processing"].includes(
                      selectedTicket.status,
                    ) && (
                      <button
                        className="u-action-success"
                        onClick={resolveTicket}
                      >
                        <CheckCircle2 size={15} />
                        解决
                      </button>
                    )}
                  {canClose && selectedTicket.status === "Resolved" && (
                    <button
                      className="u-secondary-button"
                      onClick={closeTicket}
                    >
                      关闭工单
                    </button>
                  )}
                  {(role === "Admin" ||
                    role === "Support" ||
                    role === "Customer") &&
                    ["Resolved", "Closed"].includes(selectedTicket.status) && (
                      <button
                        className="u-secondary-button"
                        onClick={reopenTicket}
                      >
                        <RotateCcw size={15} />
                        重新打开
                      </button>
                    )}
                </div>
              </div>

              <div className="u-conversation-scroll">
                <article className="u-message u-message-customer">
                  <div className="u-message-avatar">
                    {selectedTicket.createdByName?.charAt(0) || "客"}
                  </div>
                  <div className="u-message-body">
                    <div className="u-message-head">
                      <strong>{selectedTicket.createdByName}</strong>
                      <span>
                        提交问题 · {formatDateTime(selectedTicket.createdAt)}
                      </span>
                    </div>
                    <div className="u-message-content">
                      {selectedTicket.description}
                    </div>
                    <AttachmentList
                      attachments={attachments.filter(
                        (x) => x.ticketRecordId === null,
                      )}
                      onDownload={downloadAttachment}
                    />
                  </div>
                </article>

                {records.map((record) => (
                  <RecordMessage
                    key={record.id}
                    record={record}
                    attachments={attachments.filter(
                      (x) => x.ticketRecordId === record.id,
                    )}
                    onDownload={downloadAttachment}
                  />
                ))}
              </div>

              {selectedTicket.status !== "Closed" && (
                <div className="u-composer">
                  {!isCustomer && (
                    <div className="u-composer-tabs">
                      <button
                        type="button"
                        className={composerMode === "public" ? "is-active" : ""}
                        onClick={() => setComposerMode("public")}
                      >
                        公开回复
                      </button>
                      <button
                        type="button"
                        className={
                          composerMode === "internal"
                            ? "is-active is-internal"
                            : ""
                        }
                        onClick={() => setComposerMode("internal")}
                      >
                        内部备注
                      </button>
                    </div>
                  )}
                  {composerMode === "internal" && !isCustomer && (
                    <div className="u-internal-hint">
                      <ShieldAlert size={15} />
                      内部备注仅公司内部人员可见，客户看不到。
                    </div>
                  )}
                  <textarea
                    value={replyText}
                    onChange={(e) => setReplyText(e.target.value)}
                    placeholder={
                      composerMode === "internal"
                        ? "记录内部排查思路、交接信息..."
                        : "输入给客户的回复或处理进展..."
                    }
                  />
                  {replyFiles.length > 0 && (
                    <div className="u-file-chips">
                      {replyFiles.map((file, index) => (
                        <span key={`${file.name}-${index}`}>
                          {file.name}
                          <button
                            onClick={() =>
                              setReplyFiles((files) =>
                                files.filter((_, i) => i !== index),
                              )
                            }
                          >
                            <X size={12} />
                          </button>
                        </span>
                      ))}
                    </div>
                  )}
                  <div className="u-composer-foot">
                    <label className="u-attach-button">
                      <Paperclip size={16} />
                      添加附件
                      <input
                        type="file"
                        multiple
                        hidden
                        onChange={(e) =>
                          setReplyFiles(
                            (Array.from(e.target.files ?? []) as File[]).slice(
                              0,
                              5,
                            ),
                          )
                        }
                      />
                    </label>
                    <button
                      type="button"
                      className="primary-button"
                      disabled={sending || !replyText.trim()}
                      onClick={submitReply}
                    >
                      <Send size={15} />
                      {sending ? "发送中..." : "发送"}
                    </button>
                  </div>
                </div>
              )}
            </>
          )}
        </main>

        <aside className="u-ticket-info">
          {!selectedTicket ? (
            <div className="u-ticket-placeholder small">工单信息</div>
          ) : (
            <>
              <div className="u-info-block">
                <h4>基本信息</h4>
                <InfoRow label="客户" value={selectedTicket.customerName} />
                <InfoRow label="软件" value={selectedTicket.softwareName} />
                <InfoRow
                  label="来源"
                  value={getSourceName(selectedTicket.source)}
                />
                <InfoRow
                  label="状态"
                  value={getStatusName(selectedTicket.status)}
                />
                <InfoRow
                  label="负责人"
                  value={selectedTicket.assignedToName || "未分配"}
                />
                <InfoRow
                  label="创建时间"
                  value={formatDateTime(selectedTicket.createdAt)}
                />
              </div>

              {!isCustomer && (
                <div className="u-info-block">
                  <h4>服务等级</h4>
                  <InfoRow
                    label="优先级"
                    value={getPriorityName(selectedTicket.priority)}
                  />
                  {meta?.slaPriority ? (
                    <>
                      <InfoRow
                        label="响应截止"
                        value={formatDateTime(meta.responseDeadline)}
                        danger={meta.responseOverdue}
                      />
                      <InfoRow
                        label="解决截止"
                        value={formatDateTime(meta.resolutionDeadline)}
                        danger={meta.resolutionOverdue}
                      />
                    </>
                  ) : (
                    <div className="u-triage-empty">
                      <AlertTriangle size={16} />
                      {selectedTicket.priority === "Unclassified"
                        ? "等待售后分诊后应用 SLA"
                        : "当前优先级未启用 SLA"}
                    </div>
                  )}
                </div>
              )}

              {canTriage && selectedTicket.status !== "Closed" && (
                <div className="u-info-block u-triage-card">
                  <h4>分诊与分配</h4>
                  <label>
                    优先级
                    <select
                      value={triagePriority}
                      onChange={(e) => setTriagePriority(e.target.value)}
                    >
                      <option value="Low">低</option>
                      <option value="Normal">普通</option>
                      <option value="High">高</option>
                      <option value="Urgent">紧急</option>
                    </select>
                  </label>
                  <label>
                    处理人
                    <select
                      value={triageAssigneeId}
                      onChange={(e) => setTriageAssigneeId(e.target.value)}
                    >
                      <option value="">暂不分配</option>
                      {intakeOptions?.assignees.map((person) => (
                        <option value={person.id} key={person.id}>
                          {person.displayName} ·{" "}
                          {person.role === "Developer" ? "开发" : "售后"}
                        </option>
                      ))}
                    </select>
                  </label>
                  <button
                    className="primary-button"
                    disabled={triaging}
                    onClick={triageTicket}
                  >
                    <UserRoundCheck size={15} />
                    {selectedTicket.priority === "Unclassified"
                      ? "完成分诊"
                      : "更新分诊"}
                  </button>
                </div>
              )}
            </>
          )}
        </aside>
      </div>

      {showNewTicket && isCustomer && (
        <Modal
          title="提交新问题"
          subtitle="不需要判断紧急程度，售后收到后会统一分诊。"
          onClose={() => setShowNewTicket(false)}
        >
          <div className="u-form-grid">
            <label className="u-field u-field-wide">
              问题软件
              <select
                value={customerSoftwareId}
                onChange={(e) => setCustomerSoftwareId(e.target.value)}
              >
                <option value="">请选择软件</option>
                {mySoftware.map((item) => (
                  <option key={item.softwareId} value={item.softwareId}>
                    {item.softwareName} ({item.softwareCode})
                  </option>
                ))}
              </select>
            </label>
            <label className="u-field u-field-wide">
              问题标题
              <input
                value={newTitle}
                onChange={(e) => setNewTitle(e.target.value)}
                placeholder="例如：软件启动后提示数据库连接失败"
              />
            </label>
            <label className="u-field u-field-wide">
              问题描述
              <textarea
                value={newDescription}
                onChange={(e) => setNewDescription(e.target.value)}
                rows={7}
                placeholder="建议描述：出现什么问题、如何复现、当前版本、错误提示等"
              />
            </label>
            <label className="u-field u-field-wide u-file-drop">
              <Paperclip size={18} />
              添加截图、日志或其他附件（最多5个）
              <input
                type="file"
                multiple
                onChange={(e) =>
                  setNewFiles(
                    (Array.from(e.target.files ?? []) as File[]).slice(0, 5),
                  )
                }
              />
            </label>
            {newFiles.length > 0 && (
              <div className="u-file-chips u-field-wide">
                {newFiles.map((f) => (
                  <span key={f.name}>{f.name}</span>
                ))}
              </div>
            )}
          </div>
          <div className="u-modal-actions">
            <button
              className="u-secondary-button"
              onClick={() => setShowNewTicket(false)}
            >
              取消
            </button>
            <button
              className="primary-button"
              disabled={creating}
              onClick={createCustomerTicket}
            >
              {creating ? "提交中..." : "提交问题"}
            </button>
          </div>
        </Modal>
      )}

      {showStaffCreate && canTriage && (
        <Modal
          title="代客户录入工单"
          subtitle="适用于客户通过微信、电话、邮件或现场反馈的问题。"
          onClose={() => setShowStaffCreate(false)}
        >
          <div className="u-form-grid">
            <label className="u-field">
              客户
              <select
                value={staffCustomerId}
                onChange={(e) => {
                  setStaffCustomerId(e.target.value);
                  setStaffSoftwareId("");
                }}
              >
                <option value="">请选择客户</option>
                {intakeOptions?.customers.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name} ({item.code})
                  </option>
                ))}
              </select>
            </label>
            <label className="u-field">
              软件
              <select
                value={staffSoftwareId}
                onChange={(e) => setStaffSoftwareId(e.target.value)}
                disabled={!staffSelectedCustomer}
              >
                <option value="">请选择软件</option>
                {staffSelectedCustomer?.softwares.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                  </option>
                ))}
              </select>
            </label>
            <label className="u-field">
              来源
              <select
                value={staffSource}
                onChange={(e) => setStaffSource(e.target.value)}
              >
                {intakeOptions?.sources.map((item) => (
                  <option key={item} value={item}>
                    {getSourceName(item)}
                  </option>
                ))}
              </select>
            </label>
            <label className="u-field">
              优先级
              <select
                value={staffPriority}
                onChange={(e) => setStaffPriority(e.target.value)}
              >
                <option value="Low">低</option>
                <option value="Normal">普通</option>
                <option value="High">高</option>
                <option value="Urgent">紧急</option>
              </select>
            </label>
            <label className="u-field u-field-wide">
              标题
              <input
                value={staffTitle}
                onChange={(e) => setStaffTitle(e.target.value)}
              />
            </label>
            <label className="u-field u-field-wide">
              问题描述
              <textarea
                rows={6}
                value={staffDescription}
                onChange={(e) => setStaffDescription(e.target.value)}
              />
            </label>
            <label className="u-field u-field-wide">
              直接分配
              <select
                value={staffAssigneeId}
                onChange={(e) => setStaffAssigneeId(e.target.value)}
              >
                <option value="">先不分配</option>
                {intakeOptions?.assignees.map((person) => (
                  <option key={person.id} value={person.id}>
                    {person.displayName} ·{" "}
                    {person.role === "Developer" ? "开发" : "售后"}
                  </option>
                ))}
              </select>
            </label>
            <label className="u-field u-field-wide u-file-drop">
              <Paperclip size={18} />
              附上客户发来的截图、日志或文件（最多5个）
              <input
                type="file"
                multiple
                onChange={(e) =>
                  setStaffFiles(
                    (Array.from(e.target.files ?? []) as File[]).slice(0, 5),
                  )
                }
              />
            </label>
            {staffFiles.length > 0 && (
              <div className="u-file-chips u-field-wide">
                {staffFiles.map((file, index) => (
                  <span key={`${file.name}-${index}`}>
                    {file.name}
                    <button
                      type="button"
                      onClick={() =>
                        setStaffFiles((files) =>
                          files.filter((_, i) => i !== index),
                        )
                      }
                    >
                      <X size={12} />
                    </button>
                  </span>
                ))}
              </div>
            )}
          </div>
          <div className="u-modal-actions">
            <button
              className="u-secondary-button"
              onClick={() => setShowStaffCreate(false)}
            >
              取消
            </button>
            <button
              className="primary-button"
              disabled={creating}
              onClick={createStaffTicket}
            >
              {creating ? "创建中..." : "创建工单"}
            </button>
          </div>
        </Modal>
      )}
    </div>
  );
}

function RecordMessage({
  record,
  attachments,
  onDownload,
}: {
  record: TicketRecordItem;
  attachments: TicketAttachmentItem[];
  onDownload: (item: TicketAttachmentItem) => void;
}) {
  const isSystem = record.recordType !== "Comment";
  const isCustomerRecord = record.createdByRole === "Customer";

  if (isSystem) {
    return (
      <div className="u-system-event">
        <span>{getRecordIcon(record.recordType)}</span>
        <div>
          <strong>{getRecordName(record.recordType)}</strong>
          <p>{record.content}</p>
          <small>
            {record.createdByName} · {formatDateTime(record.createdAt)}
          </small>
        </div>
      </div>
    );
  }

  return (
    <article
      className={`u-message ${record.isInternal ? "u-message-internal" : isCustomerRecord ? "u-message-customer" : "u-message-staff"}`}
    >
      <div className="u-message-avatar">
        {record.createdByName?.charAt(0) || "U"}
      </div>
      <div className="u-message-body">
        <div className="u-message-head">
          <strong>{record.createdByName}</strong>
          <span>
            {record.isInternal
              ? "内部备注"
              : record.createdByRole === "Developer"
                ? "开发回复"
                : record.createdByRole === "Support"
                  ? "售后回复"
                  : "客户补充"}{" "}
            · {formatDateTime(record.createdAt)}
          </span>
        </div>
        <div className="u-message-content">{record.content}</div>
        <AttachmentList attachments={attachments} onDownload={onDownload} />
      </div>
    </article>
  );
}

function AttachmentList({
  attachments,
  onDownload,
}: {
  attachments: TicketAttachmentItem[];
  onDownload: (item: TicketAttachmentItem) => void;
}) {
  if (attachments.length === 0) return null;
  return (
    <div className="u-attachment-list">
      {attachments.map((item) => (
        <button type="button" key={item.id} onClick={() => onDownload(item)}>
          <FileText size={15} />
          <span>
            {item.fileName}
            <small>{formatFileSize(item.fileSize)}</small>
          </span>
          <Download size={14} />
        </button>
      ))}
    </div>
  );
}

function InfoRow({
  label,
  value,
  danger = false,
}: {
  label: string;
  value: string;
  danger?: boolean;
}) {
  return (
    <div className="u-info-row">
      <span>{label}</span>
      <strong className={danger ? "is-danger" : ""}>{value}</strong>
    </div>
  );
}

function Modal({
  title,
  subtitle,
  children,
  onClose,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
  onClose: () => void;
}) {
  return (
    <div
      className="u-modal-backdrop"
      onMouseDown={(e) => e.target === e.currentTarget && onClose()}
    >
      <div className="u-modal">
        <div className="u-modal-head">
          <div>
            <h3>{title}</h3>
            {subtitle && <p>{subtitle}</p>}
          </div>
          <button type="button" onClick={onClose}>
            <X size={19} />
          </button>
        </div>
        {children}
      </div>
    </div>
  );
}

function getRecordName(type: string) {
  switch (type) {
    case "Assign":
      return "工单分配";
    case "Resolve":
      return "问题已解决";
    case "Close":
      return "工单已关闭";
    case "Reopen":
      return "工单重新打开";
    case "System":
      return "系统记录";
    default:
      return type;
  }
}

function getRecordIcon(type: string) {
  switch (type) {
    case "Resolve":
      return "✓";
    case "Close":
      return "■";
    case "Reopen":
      return "↻";
    case "Assign":
      return "→";
    default:
      return "•";
  }
}

export default TicketPage;
