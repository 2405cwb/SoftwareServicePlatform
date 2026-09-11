import { useEffect, useState } from "react";
import SearchBar from "../components/SearchBar";
import { apiFetch } from "../services/api";
/*
 * 当前登录用户。
 *
 * 数据来自：
 * sessionStorage["current_user"]
 */
interface CurrentUser {
  id: number;

  username: string;

  displayName: string;

  role: string;

  customerId: number | null;
}
/*
 * GET /api/tickets
 * 返回的单条工单数据结构。
 */
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

  createdAt: string;

  updatedAt: string;

  resolvedAt: string | null;
}
/*
 * 单条工单处理记录。
 *
 * 对应：
 *
 * GET /api/tickets/{id}/records
 */
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

/*
 * 可以作为工单处理人的用户。
 *
 * 来源：
 * GET /api/tickets/assignable-users
 */
interface AssignableUser {
  id: number;

  username: string;

  displayName: string;

  role: string;
} /*
 * 当前客户已经授权的软件。
 *
 * 来源：
 * GET /api/my-software
 *
 * 提交工单时，
 * 客户只能选择自己拥有的软件。
 */
interface MySoftwareOption {
  softwareId: number;

  softwareName: string;

  softwareCode: string;
}

/*
 * 工单附件。
 *
 * 来源：
 * GET /api/tickets/{ticketId}/attachments
 */
interface TicketAttachmentItem {
  id: number;

  ticketId: number;

  /*
   * null：
   * 属于工单本身，例如首次提交的附件。
   *
   * 有值：
   * 属于某一条 TicketRecord。
   */
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

function TicketPage() {
  /*
   * 后端返回的全部工单。
   */
  const [tickets, setTickets] = useState<TicketItem[]>([]);

  /*
   * 搜索框内容。
   */
  const [searchKeyword, setSearchKeyword] = useState("");

  /*
   * 是否正在加载。
   */
  const [isLoading, setIsLoading] = useState(false);

  /*
   * 加载失败提示。
   */
  const [errorMessage, setErrorMessage] = useState("");
  /*
   * 当前正在查看的工单。
   *
   * null：
   * 当前没有打开工单详情。
   */
  const [selectedTicket, setSelectedTicket] = useState<TicketItem | null>(null);

  /*
   * 当前工单的处理记录。
   */
  const [ticketRecords, setTicketRecords] = useState<TicketRecordItem[]>([]);

  /*
   * 是否正在加载处理记录。
   */
  const [isLoadingRecords, setIsLoadingRecords] = useState(false);
  /*
   * 可分配的 Support / Developer 用户。
   */
  const [assignableUsers, setAssignableUsers] = useState<AssignableUser[]>([]);

  /*
   * 当前下拉框选择的处理人ID。
   *
   * 空字符串：
   * 还没有选择。
   */
  const [selectedAssigneeId, setSelectedAssigneeId] = useState("");

  /*
   * 是否显示工单分配区域。
   */
  const [showAssignPanel, setShowAssignPanel] = useState(false);

  /*
   * 当前是否正在提交分配操作。
   */
  const [isAssigning, setIsAssigning] = useState(false);
  /*
   * ==========================================
   * Customer 创建工单
   * ==========================================
   */

  /*
   * 是否显示创建工单表单。
   */
  const [showCreateTicket, setShowCreateTicket] = useState(false);

  /*
   * 当前客户拥有的软件。
   */
  const [mySoftwareList, setMySoftwareList] = useState<MySoftwareOption[]>([]);

  /*
   * 当前选择的软件ID。
   *
   * select 的 value 是 string，
   * 提交时再 Number()。
   */
  const [newTicketSoftwareId, setNewTicketSoftwareId] = useState("");

  /*
   * 工单标题。
   */
  const [newTicketTitle, setNewTicketTitle] = useState("");

  /*
   * 问题描述。
   */
  const [newTicketDescription, setNewTicketDescription] = useState("");

  /*
   * 优先级。
   */
  const [newTicketPriority, setNewTicketPriority] = useState("Normal");

  /*
   * 是否正在加载客户软件。
   */
  const [isLoadingMySoftware, setIsLoadingMySoftware] = useState(false);

  /*
   * 是否正在提交工单。
   */
  const [isCreatingTicket, setIsCreatingTicket] = useState(false);
  /*
   * 页面第一次进入时加载工单。
   */

  /*
   * 客户提交新工单时选择的附件。
   */
  const [newTicketFiles, setNewTicketFiles] = useState<File[]>([]);

  /*
   * 当前工单全部有权限查看的附件。
   */
  const [ticketAttachments, setTicketAttachments] = useState<
    TicketAttachmentItem[]
  >([]);

  /*
   * 回复工单时准备上传的附件。
   */
  const [recordFiles, setRecordFiles] = useState<File[]>([]);

  /*
   * 是否正在加载附件。
   */
  const [isLoadingAttachments, setIsLoadingAttachments] = useState(false);

  useEffect(() => {
    loadTickets();
  }, []);
  /*
   * ==========================================
   * 当前登录用户
   * ==========================================
   */

  let currentUser: CurrentUser | null = null;

  const currentUserJson = sessionStorage.getItem("current_user");

  if (currentUserJson) {
    try {
      currentUser = JSON.parse(currentUserJson) as CurrentUser;
    } catch (error) {
      console.error("解析当前用户失败：", error);
    }
  }

  /*
   * 当前角色。
   */
  const currentRole = currentUser?.role ?? "";

  /*
   * 是否管理员 / 售后。
   */
  const canManageTicket = currentRole === "Admin" || currentRole === "Support";

  /*
   * 是否开发人员。
   */
  const isDeveloper = currentRole === "Developer";

  /*
   * 是否客户。
   */
  const isCustomer = currentRole === "Customer";
  /*
   * ==========================================
   * 加载工单列表
   * ==========================================
   */

  /*
   * ==========================================
   * 工单回复 / 处理记录
   * ==========================================
   */

  /*
   * 当前准备提交的回复内容。
   */
  const [recordContent, setRecordContent] = useState("");

  /*
   * 是否作为内部记录。
   *
   * Customer 不能创建内部记录。
   *
   * Admin / Support / Developer
   * 可以选择。
   */
  const [recordIsInternal, setRecordIsInternal] = useState(false);

  /*
   * 当前是否正在提交处理记录。
   */
  const [isSubmittingRecord, setIsSubmittingRecord] = useState(false);
  /*
   * ==========================================
   * 工单状态操作
   * ==========================================
   */

  /*
   * 解决工单时填写的解决说明。
   */
  const [resolveContent, setResolveContent] = useState("");

  /*
   * 重新打开工单时填写的原因。
   */
  const [reopenContent, setReopenContent] = useState("");

  /*
   * 当前是否正在执行状态操作。
   *
   * 防止用户连续点击按钮。
   */
  const [isChangingTicketStatus, setIsChangingTicketStatus] = useState(false);
  async function loadTickets() {
    try {
      setIsLoading(true);

      setErrorMessage("");

      /*
       * apiFetch 会自动携带 JWT。
       */
      const response = await apiFetch("/api/tickets");

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取工单列表失败：${response.status}`);
      }

      /*
       * 告诉 TypeScript：
       *
       * 后端返回的是 TicketItem 数组。
       *
       * 注意：
       * as TicketItem[] 保持在这一行。
       */
      const data = (await response.json()) as TicketItem[];

      setTickets(data);
    } catch (error) {
      console.error("加载工单列表失败：", error);

      setErrorMessage("加载工单列表失败");
    } finally {
      setIsLoading(false);
    }
  }
  /*
   * ==========================================
   * 打开创建工单界面
   * ==========================================
   */
  async function openCreateTicket() {
    /*
     * 只有 Customer 才允许使用。
     *
     * 前端判断只是用户体验。
     * 后端 POST /api/tickets
     * 仍然有 Customer 权限检查。
     */
    if (!isCustomer) {
      return;
    }

    /*
     * 每次打开都清空上一次填写内容。
     */
    setNewTicketSoftwareId("");

    setNewTicketTitle("");

    setNewTicketDescription("");

    setNewTicketPriority("Normal");
    setNewTicketFiles([]);
    /*
     * 先显示表单。
     */
    setShowCreateTicket(true);

    try {
      setIsLoadingMySoftware(true);

      /*
       * 查询当前客户真正拥有的软件。
       *
       * 注意：
       *
       * 这里绝对不要：
       *
       * /api/my-software?customerId=xxx
       *
       * 当前客户是谁由后端 JWT 决定。
       */
      const response = await apiFetch("/api/my-software");

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取客户软件失败：${response.status}`);
      }

      const data = (await response.json()) as MySoftwareOption[];

      setMySoftwareList(data);

      /*
       * 如果客户只有一个软件，
       * 自动帮他选中。
       */
      if (data.length === 1) {
        setNewTicketSoftwareId(String(data[0].softwareId));
      }
    } catch (error) {
      console.error("加载客户软件失败：", error);

      alert("加载客户软件失败：" + String(error));
    } finally {
      setIsLoadingMySoftware(false);
    }
  } /*
   * ==========================================
   * Customer 提交工单
   * ==========================================
   */
  async function createTicket() {
    /*
     * 当前必须是 Customer。
     */
    if (!isCustomer) {
      return;
    }

    /*
     * 软件必须选择。
     */
    if (newTicketSoftwareId === "") {
      alert("请选择出现问题的软件");

      return;
    }

    /*
     * 标题不能为空。
     */
    if (newTicketTitle.trim() === "") {
      alert("请输入问题标题");

      return;
    }

    /*
     * 问题描述不能为空。
     */
    if (newTicketDescription.trim() === "") {
      alert("请输入问题描述");

      return;
    }

    try {
      setIsCreatingTicket(true);

      /*
       * 调用之前已经完成的后端接口：
       *
       * POST /api/tickets
       */
      const response = await apiFetch("/api/tickets", {
        method: "POST",

        headers: {
          "Content-Type": "application/json",
        },

        body: JSON.stringify({
          softwareId: Number(newTicketSoftwareId),

          title: newTicketTitle.trim(),

          description: newTicketDescription.trim(),

          priority: newTicketPriority,
        }),
      });

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `提交工单失败：${response.status}`);
      }

      /*
       * 后端返回新创建的工单信息。
       */
      const result = await response.json();

      console.log("工单创建成功：", result);
      /*
       * ==========================================
       * 上传工单附件
       * ==========================================
       *
       * Ticket 已经创建成功，
       * result.id 就是刚生成的 TicketId。
       */
      let attachmentUploadError = "";

      if (newTicketFiles.length > 0) {
        try {
          /*
           * 上传文件不能使用 JSON。
           *
           * 要使用 FormData，
           * 浏览器会自动生成：
           *
           * multipart/form-data
           */
          const formData = new FormData();

          for (const file of newTicketFiles) {
            /*
             * 后端参数名叫 files，
             * 所以每一个都 append("files", ...)
             */
            formData.append("files", file);
          }

          /*
           * Customer 上传的一定是公开附件。
           */
          formData.append("isInternal", "false");

          const attachmentResponse = await apiFetch(
            `/api/tickets/${result.id}/attachments`,
            {
              method: "POST",

              /*
               * 注意：
               *
               * 这里千万不要手工写：
               *
               * Content-Type: multipart/form-data
               *
               * 浏览器会自动添加 boundary。
               */
              body: formData,
            },
          );

          if (!attachmentResponse.ok) {
            const errorText = await attachmentResponse.text();

            attachmentUploadError =
              errorText || `附件上传失败：${attachmentResponse.status}`;
          }
        } catch (error) {
          attachmentUploadError = String(error);
        }
      }
      /*
       * Ticket 已经创建完成，
       * 不管附件是否成功，
       * 都关闭创建区域。
       */
      setShowCreateTicket(false);

      setNewTicketSoftwareId("");

      setNewTicketTitle("");

      setNewTicketDescription("");

      setNewTicketPriority("Normal");

      setNewTicketFiles([]);

      /*
       * 刷新客户工单列表。
       */
      await loadTickets();

      /*
       * Ticket 创建成功，
       * 但附件有可能上传失败。
       *
       * 这里不能错误提示：
       *
       * “工单提交失败”
       *
       * 因为 Ticket 实际已经创建成功。
       */
      if (attachmentUploadError) {
        alert(
          `工单已经提交成功\n` +
            `工单号：${result.ticketNo}\n\n` +
            `但附件上传失败：\n` +
            attachmentUploadError,
        );
      } else {
        alert(`工单提交成功\n` + `工单号：${result.ticketNo}`);
      }
    } catch (error) {
      console.error("提交工单失败：", error);

      alert("提交工单失败：" + String(error));
    } finally {
      setIsCreatingTicket(false);
    }
  }

  /*
   * ==========================================
   * 选择新工单附件
   * ==========================================
   *
   * 与后端规则保持一致：
   *
   * 一次最多 5 个
   * 单文件最大 100MB
   */
  function selectNewTicketFiles(fileList: FileList | null) {
    if (fileList === null) {
      return;
    }

    const selectedFiles = Array.from(fileList);

    /*
     * 客户可能分两次选择文件，
     * 所以要和已经选择的文件合并。
     */
    const nextFiles = [...newTicketFiles];

    for (const file of selectedFiles) {
      /*
       * 最多5个。
       */
      if (nextFiles.length >= 5) {
        alert("一个工单最多上传5个附件");

        break;
      }

      /*
       * 单文件100MB。
       */
      const maxFileSize = 100 * 1024 * 1024;

      if (file.size > maxFileSize) {
        alert(`文件 ${file.name} 超过100MB，已跳过`);

        continue;
      }

      /*
       * 前端先挡掉明显不允许的文件。
       *
       * 后端仍然会再次检查。
       */
      const extensionIndex = file.name.lastIndexOf(".");

      const extension =
        extensionIndex >= 0
          ? file.name.substring(extensionIndex).toLowerCase()
          : "";

      const blockedExtensions = [
        ".exe",
        ".dll",
        ".msi",
        ".bat",
        ".cmd",
        ".com",
        ".scr",
        ".ps1",
        ".vbs",
        ".js",
        ".reg",
      ];

      if (blockedExtensions.includes(extension)) {
        alert(`不允许上传 ${extension} 类型的文件`);

        continue;
      }

      /*
       * 防止重复选择完全相同的文件。
       */
      const duplicated = nextFiles.some(
        (currentFile) =>
          currentFile.name === file.name &&
          currentFile.size === file.size &&
          currentFile.lastModified === file.lastModified,
      );

      if (!duplicated) {
        nextFiles.push(file);
      }
    }

    setNewTicketFiles(nextFiles);
  }

  /*
   * 从待上传附件列表中移除一个文件。
   */
  function removeNewTicketFile(fileIndex: number) {
    setNewTicketFiles((currentFiles) =>
      currentFiles.filter((_, index) => index !== fileIndex),
    );
  }

  /*
   * 把字节转换成人容易阅读的大小。
   */
  function formatFileSize(bytes: number) {
    if (bytes < 1024) {
      return `${bytes} B`;
    }

    const kb = bytes / 1024;

    if (kb < 1024) {
      return `${kb.toFixed(1)} KB`;
    }

    const mb = kb / 1024;

    return `${mb.toFixed(1)} MB`;
  }
  /*
   * ==========================================
   * 刷新当前工单
   * ==========================================
   *
   * 用于：
   *
   * 分配
   * Resolve
   * Close
   * Reopen
   *
   * 操作完成以后同步刷新：
   *
   * 1. 工单列表
   * 2. 当前工单详情
   * 3. 工单时间线
   */
  async function refreshCurrentTicket(ticketId: number) {
    try {
      /*
       * 重新获取当前用户能看到的全部工单。
       */
      const response = await apiFetch("/api/tickets");

      if (!response.ok) {
        throw new Error(`刷新工单失败：${response.status}`);
      }

      const data = (await response.json()) as TicketItem[];

      setTickets(data);

      /*
       * 从最新数据中找到当前 Ticket。
       */
      const latestTicket = data.find((ticket) => ticket.id === ticketId);

      if (latestTicket) {
        setSelectedTicket(latestTicket);
      }

      await Promise.all([
        loadTicketRecords(ticketId),

        loadTicketAttachments(ticketId),
      ]);
    } catch (error) {
      console.error("刷新当前工单失败：", error);
    }
  }

  /*
   * ==========================================
   * 查看工单
   * ==========================================
   */

  /*
   * ==========================================
   * 加载指定工单的处理记录
   * ==========================================
   *
   * 为什么单独抽成一个方法：
   *
   * 查看工单
   * 分配工单
   * 回复工单
   * Resolve
   * Close
   * Reopen
   *
   * 这些操作完成以后都需要刷新时间线。
   */
  async function loadTicketRecords(ticketId: number) {
    try {
      setIsLoadingRecords(true);

      const response = await apiFetch(`/api/tickets/${ticketId}/records`);

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(
          errorText || `获取工单处理记录失败：${response.status}`,
        );
      }

      const data = (await response.json()) as TicketRecordItem[];

      setTicketRecords(data);
    } catch (error) {
      console.error("加载工单处理记录失败：", error);

      alert("加载工单处理记录失败：" + String(error));
    } finally {
      setIsLoadingRecords(false);
    }
  }
  /*
   * ==========================================
   * 加载工单附件
   * ==========================================
   */
  async function loadTicketAttachments(ticketId: number) {
    try {
      setIsLoadingAttachments(true);

      const response = await apiFetch(`/api/tickets/${ticketId}/attachments`);

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取工单附件失败：${response.status}`);
      }

      const data = (await response.json()) as TicketAttachmentItem[];

      setTicketAttachments(data);
    } catch (error) {
      console.error("加载工单附件失败：", error);

      /*
       * 附件加载失败不阻止整个 Ticket 详情显示。
       */
      setTicketAttachments([]);
    } finally {
      setIsLoadingAttachments(false);
    }
  }
  /*
   * ==========================================
   * 提交工单处理记录
   * ==========================================
   */
  async function submitTicketRecord() {
    /*
     * 当前没有打开工单，
     * 理论上不会发生。
     */
    if (selectedTicket === null) {
      return;
    }

    /*
     * 回复内容不能为空。
     */
    if (recordContent.trim() === "") {
      alert("请输入回复内容");

      return;
    }

    /*
     * 已关闭工单不能继续回复。
     *
     * 后端本身也会检查，
     * 前端这里属于用户体验优化。
     */
    if (selectedTicket.status === "Closed") {
      alert("已关闭的工单不能继续回复");

      return;
    }

    try {
      setIsSubmittingRecord(true);
      /*
       * 调用后端新增处理记录接口。
       *
       * 注意：
       * 这里必须是 POST。
       *
       * 如果不写 method，
       * fetch 默认就是 GET，
       * 那就只是在查询时间线，
       * 根本不会新增记录。
       */
      const response = await apiFetch(
        `/api/tickets/${selectedTicket.id}/records`,
        {
          method: "POST",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            /*
             * 回复内容。
             */
            content: recordContent.trim(),

            /*
             * Customer 永远不能创建内部记录。
             *
             * Admin / Support / Developer
             * 根据复选框决定。
             */
            isInternal: isCustomer ? false : recordIsInternal,
          }),
        },
      );

      /*
       * 必须先判断 HTTP 是否成功。
       *
       * 例如：
       *
       * 400 内容为空
       * 403 没有权限
       * 401 登录失效
       */
      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `提交处理记录失败：${response.status}`);
      }

      /*
       * 后端返回刚创建的 TicketRecord。
       *
       * result.id 后面还要给附件使用。
       */
      const result = await response.json();

      /*
       * TicketRecord 已经成功创建。
       *
       * 如果用户同时选择了附件，
       * 再把附件关联到这个 Record。
       */
      let attachmentUploadError = "";

      if (recordFiles.length > 0) {
        try {
          const formData = new FormData();

          for (const file of recordFiles) {
            formData.append("files", file);
          }

          /*
           * 与处理记录保持相同可见性。
           *
           * Customer 永远 false。
           */
          formData.append(
            "isInternal",
            isCustomer ? "false" : String(recordIsInternal),
          );

          /*
           * 关键：
           *
           * 附件属于刚刚创建的 TicketRecord。
           */
          formData.append("ticketRecordId", String(result.id));

          const attachmentResponse = await apiFetch(
            `/api/tickets/${selectedTicket.id}/attachments`,
            {
              method: "POST",

              body: formData,
            },
          );

          if (!attachmentResponse.ok) {
            const errorText = await attachmentResponse.text();

            attachmentUploadError =
              errorText || `附件上传失败：${attachmentResponse.status}`;
          }
        } catch (error) {
          attachmentUploadError = String(error);
        }
      }

      /*
       * 清空输入。
       */
      setRecordContent("");

      setRecordIsInternal(false);

      setRecordFiles([]);

      /*
       * 回复 + 附件都完成后，
       * 一次刷新时间线和附件。
       */
      await Promise.all([
        loadTicketRecords(selectedTicket.id),

        loadTicketAttachments(selectedTicket.id),
      ]);

      if (attachmentUploadError) {
        alert(
          "处理记录已经提交成功，" +
            "但附件上传失败：\n" +
            attachmentUploadError,
        );
      }
    } catch (error) {
      console.error("提交工单处理记录失败：", error);

      alert("提交工单处理记录失败：" + String(error));
    } finally {
      setIsSubmittingRecord(false);
    }
  }

  /*
   * ==========================================
   * 标记工单已解决
   * ==========================================
   */
  async function resolveTicket() {
    if (selectedTicket === null) {
      return;
    }

    /*
     * 解决说明不能为空。
     */
    if (resolveContent.trim() === "") {
      alert("请输入问题解决说明");

      return;
    }

    try {
      setIsChangingTicketStatus(true);

      const response = await apiFetch(
        `/api/tickets/${selectedTicket.id}/resolve`,
        {
          method: "PUT",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            content: resolveContent.trim(),
          }),
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `解决工单失败：${response.status}`);
      }

      /*
       * 清空解决说明。
       */
      setResolveContent("");

      /*
       * 后端会：
       *
       * Status = Resolved
       * ResolvedAt = now
       *
       * 同时新增：
       *
       * RecordType = Resolve
       */
      await refreshCurrentTicket(selectedTicket.id);

      alert("工单已标记为解决");
    } catch (error) {
      console.error("解决工单失败：", error);

      alert("解决工单失败：" + String(error));
    } finally {
      setIsChangingTicketStatus(false);
    }
  } /*
   * ==========================================
   * 关闭工单
   * ==========================================
   */
  async function closeTicket() {
    if (selectedTicket === null) {
      return;
    }

    /*
     * 给用户二次确认。
     */
    const confirmed = window.confirm("确认问题已经解决并关闭这个工单吗？");

    if (!confirmed) {
      return;
    }

    try {
      setIsChangingTicketStatus(true);

      const response = await apiFetch(
        `/api/tickets/${selectedTicket.id}/close`,
        {
          method: "PUT",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            content: isCustomer
              ? "客户确认问题已解决，关闭工单"
              : "工单处理完成并关闭",
          }),
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `关闭工单失败：${response.status}`);
      }

      /*
       * 后端会：
       *
       * Status = Closed
       *
       * 同时新增：
       *
       * RecordType = Close
       */
      await refreshCurrentTicket(selectedTicket.id);

      alert("工单已关闭");
    } catch (error) {
      console.error("关闭工单失败：", error);

      alert("关闭工单失败：" + String(error));
    } finally {
      setIsChangingTicketStatus(false);
    }
  } /*
   * ==========================================
   * 重新打开工单
   * ==========================================
   */
  async function reopenTicket() {
    if (selectedTicket === null) {
      return;
    }

    if (reopenContent.trim() === "") {
      alert("请输入重新打开工单的原因");

      return;
    }

    try {
      setIsChangingTicketStatus(true);

      const response = await apiFetch(
        `/api/tickets/${selectedTicket.id}/reopen`,
        {
          method: "PUT",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            content: reopenContent.trim(),
          }),
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `重新打开工单失败：${response.status}`);
      }

      setReopenContent("");

      /*
       * 后端会：
       *
       * Status = Processing
       * ResolvedAt = null
       *
       * 同时新增：
       *
       * RecordType = Reopen
       */
      await refreshCurrentTicket(selectedTicket.id);

      alert("工单已重新打开");
    } catch (error) {
      console.error("重新打开工单失败：", error);

      alert("重新打开工单失败：" + String(error));
    } finally {
      setIsChangingTicketStatus(false);
    }
  }
  async function viewTicket(ticket: TicketItem) {
    setSelectedTicket(ticket);

    /*
     * 切换 Ticket 时先清空旧数据。
     */
    setTicketRecords([]);

    setTicketAttachments([]);

    setRecordFiles([]);

    /*
     * 两个请求互相独立，
     * 可以同时执行。
     */
    await Promise.all([
      loadTicketRecords(ticket.id),

      loadTicketAttachments(ticket.id),
    ]);
  }

  /*
   * ==========================================
   * 打开工单分配区域
   * ==========================================
   */
  async function openAssignPanel() {
    if (selectedTicket === null) {
      return;
    }

    try {
      /*
       * 先加载可以作为处理人的用户。
       */
      const response = await apiFetch("/api/tickets/assignable-users");

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `获取处理人员失败：${response.status}`);
      }

      const data = (await response.json()) as AssignableUser[];

      setAssignableUsers(data);

      /*
       * 如果这个工单以前已经有处理人，
       * 下拉框默认选中当前处理人。
       */
      if (selectedTicket.assignedToUserId !== null) {
        setSelectedAssigneeId(String(selectedTicket.assignedToUserId));
      } else {
        setSelectedAssigneeId("");
      }

      setShowAssignPanel(true);
    } catch (error) {
      console.error("获取可分配处理人失败：", error);

      alert("获取可分配处理人失败：" + String(error));
    }
  }

  /*
   * ==========================================
   * 分配工单
   * ==========================================
   */
  async function assignTicket() {
    if (selectedTicket === null) {
      return;
    }

    if (selectedAssigneeId === "") {
      alert("请选择处理人");

      return;
    }

    try {
      setIsAssigning(true);

      const response = await apiFetch(
        `/api/tickets/${selectedTicket.id}/assign`,
        {
          method: "PUT",

          headers: {
            "Content-Type": "application/json",
          },

          body: JSON.stringify({
            assignedToUserId: Number(selectedAssigneeId),
          }),
        },
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `分配工单失败：${response.status}`);
      }

      /*
       * 后端 AssignTicket 当前会返回：
       *
       * status
       * assignedToUserId
       * assignedToName
       * updatedAt
       * ...
       */
      const result = await response.json();

      setShowAssignPanel(false);

      await refreshCurrentTicket(selectedTicket.id);

      alert("工单分配成功");
    } catch (error) {
      console.error("分配工单失败：", error);

      alert("分配工单失败：" + String(error));
    } finally {
      setIsAssigning(false);
    }
  }
  /*
   * ==========================================
   * 工单状态英文 → 中文
   * ==========================================
   */
  function getStatusName(status: string) {
    switch (status) {
      case "Pending":
        return "待处理";

      case "Processing":
        return "处理中";

      case "Resolved":
        return "已解决";

      case "Closed":
        return "已关闭";

      default:
        return status;
    }
  }

  /*
   * ==========================================
   * 优先级英文 → 中文
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
  function getRoleName(role: string) {
    switch (role) {
      case "Support":
        return "售后";

      case "Developer":
        return "开发";

      case "Admin":
        return "管理员";

      case "Customer":
        return "客户";

      default:
        return role;
    }
  }
  /*
   * ==========================================
   * 处理记录类型英文 → 中文
   * ==========================================
   */
  function getRecordTypeName(recordType: string) {
    switch (recordType) {
      case "Comment":
        return "处理记录";

      case "Assign":
        return "工单分配";

      case "Resolve":
        return "已解决";

      case "Close":
        return "已关闭";

      case "Reopen":
        return "重新打开";

      case "System":
        return "系统记录";

      default:
        return recordType;
    }
  }
  /*
   * ==========================================
   * 时间格式化
   * ==========================================
   *
   * 后端返回：
   *
   * 2026-09-11T01:20:30Z
   *
   * 转成浏览器本地时间。
   */
  function formatDateTime(value: string | null) {
    if (!value) {
      return "-";
    }

    const date = new Date(value);

    return date.toLocaleString();
  }

  /*
   * ==========================================
   * 前端搜索
   * ==========================================
   *
   * 当前数据量还比较少，
   * 第一版直接前端过滤即可。
   *
   * 后面数据量大以后，
   * 再学习后端搜索 + 分页。
   */
  const filteredTickets = tickets.filter((ticket) => {
    const keyword = searchKeyword.trim().toLowerCase();

    if (keyword === "") {
      return true;
    }

    return (
      ticket.ticketNo.toLowerCase().includes(keyword) ||
      ticket.title.toLowerCase().includes(keyword) ||
      ticket.customerName.toLowerCase().includes(keyword) ||
      ticket.softwareName.toLowerCase().includes(keyword) ||
      (ticket.assignedToName ?? "").toLowerCase().includes(keyword)
    );
  });
  /*
   * ==========================================
   * 下载 Ticket 附件
   * ==========================================
   */
  async function downloadTicketAttachment(attachment: TicketAttachmentItem) {
    try {
      const response = await apiFetch(
        `/api/tickets/attachments/${attachment.id}/download`,
      );

      if (!response.ok) {
        const errorText = await response.text();

        throw new Error(errorText || `附件下载失败：${response.status}`);
      }

      /*
       * 把 HTTP 文件响应读取成 Blob。
       */
      const blob = await response.blob();

      /*
       * 给 Blob 临时生成一个浏览器 URL。
       */
      const url = URL.createObjectURL(blob);

      const link = document.createElement("a");

      link.href = url;

      link.download = attachment.fileName;

      document.body.appendChild(link);

      link.click();

      link.remove();

      /*
       * 临时 URL 用完必须释放。
       */
      URL.revokeObjectURL(url);
    } catch (error) {
      console.error("下载附件失败：", error);

      alert("下载附件失败：" + String(error));
    }
  }

  /*
   * ==========================================
   * 选择回复附件
   * ==========================================
   */
  function selectRecordFiles(fileList: FileList | null) {
    if (fileList === null) {
      return;
    }

    const selectedFiles = Array.from(fileList);

    const nextFiles = [...recordFiles];

    const maxFileSize = 100 * 1024 * 1024;

    const blockedExtensions = [
      ".exe",
      ".dll",
      ".msi",
      ".bat",
      ".cmd",
      ".com",
      ".scr",
      ".ps1",
      ".vbs",
      ".js",
      ".reg",
    ];

    for (const file of selectedFiles) {
      if (nextFiles.length >= 5) {
        alert("一次最多上传5个附件");

        break;
      }

      if (file.size > maxFileSize) {
        alert(`文件 ${file.name} 超过100MB，已跳过`);

        continue;
      }

      const extensionIndex = file.name.lastIndexOf(".");

      const extension =
        extensionIndex >= 0
          ? file.name.substring(extensionIndex).toLowerCase()
          : "";

      if (blockedExtensions.includes(extension)) {
        alert(`不允许上传 ${extension} 类型的文件`);

        continue;
      }

      const duplicated = nextFiles.some(
        (currentFile) =>
          currentFile.name === file.name &&
          currentFile.size === file.size &&
          currentFile.lastModified === file.lastModified,
      );

      if (!duplicated) {
        nextFiles.push(file);
      }
    }

    setRecordFiles(nextFiles);
  }

  function removeRecordFile(fileIndex: number) {
    setRecordFiles((currentFiles) =>
      currentFiles.filter((_, index) => index !== fileIndex),
    );
  }
  return (
    <div className="content">
      {/* =============================
          页面标题
          ============================= */}
      <div className="title-row">
        <h2>
          {isCustomer ? "我的工单" : isDeveloper ? "我的处理工单" : "工单管理"}
        </h2>

        {isCustomer && (
          <button
            type="button"
            className="primary-button"
            onClick={openCreateTicket}
          >
            + 提交工单
          </button>
        )}
      </div>
      {/* =============================
    Customer 创建工单
    ============================= */}

      {isCustomer && showCreateTicket && (
        <div className="form-box">
          <h3>提交新工单</h3>

          {/* =========================
        基础信息
        ========================= */}
          <div className="form-section">
            <h4>问题信息</h4>

            <div className="form-grid">
              {/* 软件 */}
              <div className="form-item">
                <label>问题软件</label>

                <select
                  value={newTicketSoftwareId}
                  disabled={isLoadingMySoftware}
                  onChange={(e) => {
                    setNewTicketSoftwareId(e.target.value);
                  }}
                >
                  <option value="">
                    {isLoadingMySoftware ? "正在加载软件..." : "请选择软件"}
                  </option>

                  {mySoftwareList.map((software) => (
                    <option
                      value={software.softwareId}
                      key={software.softwareId}
                    >
                      {software.softwareName}
                      {" ("}
                      {software.softwareCode}
                      {")"}
                    </option>
                  ))}
                </select>
              </div>

              {/* 优先级 */}
              <div className="form-item">
                <label>优先级</label>

                <select
                  value={newTicketPriority}
                  onChange={(e) => {
                    setNewTicketPriority(e.target.value);
                  }}
                >
                  <option value="Low">低</option>

                  <option value="Normal">普通</option>

                  <option value="High">高</option>

                  <option value="Urgent">紧急</option>
                </select>
              </div>

              {/* 标题 */}
              <div className="form-item ticket-create-title">
                <label>问题标题</label>

                <input
                  type="text"
                  value={newTicketTitle}
                  maxLength={200}
                  placeholder="例如：软件启动后提示数据库连接失败"
                  onChange={(e) => {
                    setNewTicketTitle(e.target.value);
                  }}
                />
              </div>
            </div>

            {/* 问题描述 */}
            <div className="form-item-full ticket-create-description">
              <label>问题描述</label>

              <textarea
                className="remark-input"
                value={newTicketDescription}
                placeholder={
                  "请尽量详细描述问题，例如：\n" +
                  "1. 做了什么操作\n" +
                  "2. 出现什么现象\n" +
                  "3. 是否有报错提示\n" +
                  "4. 是否可以稳定复现"
                }
                onChange={(e) => {
                  setNewTicketDescription(e.target.value);
                }}
              />
            </div>
            {/* =========================
    工单附件
    ========================= */}
            <div className="ticket-create-attachments">
              <label>附件</label>

              <div className="ticket-file-picker">
                <input
                  type="file"
                  multiple
                  onChange={(e) => {
                    selectNewTicketFiles(e.target.files);

                    /*
                     * 清空 input 自身。
                     *
                     * 这样用户删除以后，
                     * 还能重新选择同一个文件。
                     */
                    e.target.value = "";
                  }}
                />

                <div className="ticket-file-tip">
                  最多5个附件，单个最大100MB。
                  支持截图、日志、PDF、Office、压缩包等文件。
                </div>
              </div>

              {/* 已选择附件 */}
              {newTicketFiles.length > 0 && (
                <div className="ticket-selected-files">
                  {newTicketFiles.map((file, index) => (
                    <div
                      className="ticket-selected-file"
                      key={`${file.name}-${file.size}-${file.lastModified}`}
                    >
                      <div className="ticket-selected-file-info">
                        <strong>{file.name}</strong>

                        <span>{formatFileSize(file.size)}</span>
                      </div>

                      <button
                        type="button"
                        className="ticket-file-remove"
                        onClick={() => {
                          removeNewTicketFile(index);
                        }}
                      >
                        移除
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* =========================
        没有授权软件
        ========================= */}
          {!isLoadingMySoftware && mySoftwareList.length === 0 && (
            <div className="ticket-create-warning">
              当前客户没有已授权软件， 暂时无法提交工单。
              请联系软件服务人员完成软件授权。
            </div>
          )}

          {/* =========================
        操作按钮
        ========================= */}
          <div className="form-buttons">
            <button
              type="button"
              className="normal-button"
              onClick={() => {
                setShowCreateTicket(false);

                setNewTicketFiles([]);
              }}
            >
              取消
            </button>

            <button
              type="button"
              className="primary-button"
              disabled={
                isCreatingTicket ||
                isLoadingMySoftware ||
                mySoftwareList.length === 0
              }
              onClick={createTicket}
            >
              {isCreatingTicket ? "正在提交..." : "提交工单"}
            </button>
          </div>
        </div>
      )}

      {/* =============================
    工单详情
    ============================= */}
      {selectedTicket !== null && (
        <div className="ticket-detail">
          {/* =========================
        详情标题
        ========================= */}
          <div className="ticket-detail-header">
            <div>
              <h3>{selectedTicket.ticketNo}</h3>

              <div className="ticket-detail-title">{selectedTicket.title}</div>
            </div>

            <button
              type="button"
              className="normal-button"
              onClick={() => {
                setSelectedTicket(null);

                setTicketRecords([]);

                setTicketAttachments([]);

                setRecordFiles([]);
              }}
            >
              关闭详情
            </button>
          </div>

          {/* =========================
        基础信息
        ========================= */}
          <div className="ticket-detail-grid">
            <div>
              <span>客户</span>

              <strong>{selectedTicket.customerName}</strong>
            </div>

            <div>
              <span>软件</span>

              <strong>{selectedTicket.softwareName}</strong>
            </div>

            <div>
              <span>状态</span>

              <strong>{getStatusName(selectedTicket.status)}</strong>
            </div>

            <div>
              <span>优先级</span>

              <strong>{getPriorityName(selectedTicket.priority)}</strong>
            </div>

            <div>
              <span>提交人</span>

              <strong>{selectedTicket.createdByName}</strong>
            </div>

            <div>
              <span>处理人</span>

              <strong>{selectedTicket.assignedToName ?? "暂未分配"}</strong>
            </div>

            <div>
              <span>创建时间</span>

              <strong>{formatDateTime(selectedTicket.createdAt)}</strong>
            </div>

            <div>
              <span>解决时间</span>

              <strong>{formatDateTime(selectedTicket.resolvedAt)}</strong>
            </div>
          </div>

          {/* =========================
        客户问题描述
        ========================= */}

          {canManageTicket && (
            <>
              <div className="ticket-manage-bar">
                <div>
                  <span>当前处理人：</span>

                  <strong>{selectedTicket.assignedToName ?? "暂未分配"}</strong>
                </div>

                <button
                  type="button"
                  className="primary-button"
                  onClick={openAssignPanel}
                >
                  {selectedTicket.assignedToUserId === null
                    ? "分配处理人"
                    : "重新分配"}
                </button>
              </div>

              {/* =========================
        工单分配区域
        ========================= */}
              {showAssignPanel && (
                <div className="ticket-assign-panel">
                  <div className="ticket-assign-field">
                    <label>处理人</label>

                    <select
                      value={selectedAssigneeId}
                      onChange={(e) => {
                        setSelectedAssigneeId(e.target.value);
                      }}
                    >
                      <option value="">请选择处理人</option>

                      {assignableUsers.map((user) => (
                        <option value={user.id} key={user.id}>
                          {user.displayName}
                          {" · "}
                          {getRoleName(user.role)}
                          {" · "}
                          {user.username}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="ticket-assign-actions">
                    <button
                      type="button"
                      className="normal-button"
                      onClick={() => {
                        setShowAssignPanel(false);
                      }}
                    >
                      取消
                    </button>

                    <button
                      type="button"
                      className="primary-button"
                      disabled={isAssigning}
                      onClick={assignTicket}
                    >
                      {isAssigning ? "正在分配..." : "确认分配"}
                    </button>
                  </div>
                </div>
              )}
            </>
          )}
          <div className="ticket-description">
            <h4>问题描述</h4>

            <div>{selectedTicket.description}</div>
          </div>
          {/* =============================
    工单原始附件
    ============================= */}

          {(isLoadingAttachments ||
            ticketAttachments.some(
              (attachment) => attachment.ticketRecordId === null,
            )) && (
            <div className="ticket-detail-attachments">
              <h4>工单附件</h4>

              {isLoadingAttachments && (
                <div className="ticket-attachment-empty">正在加载附件...</div>
              )}

              {!isLoadingAttachments &&
                ticketAttachments
                  .filter((attachment) => attachment.ticketRecordId === null)
                  .map((attachment) => (
                    <div className="ticket-attachment-item" key={attachment.id}>
                      <div className="ticket-attachment-info">
                        <strong>{attachment.fileName}</strong>

                        <span>{formatFileSize(attachment.fileSize)}</span>

                        {attachment.isInternal && (
                          <span className="ticket-internal-tag">内部</span>
                        )}
                      </div>

                      <button
                        type="button"
                        className="edit-button"
                        onClick={() => {
                          downloadTicketAttachment(attachment);
                        }}
                      >
                        下载
                      </button>
                    </div>
                  ))}
            </div>
          )}
          {/* =========================
        处理记录
        ========================= */}
          <div className="ticket-record-section">
            <h4>处理记录</h4>

            {isLoadingRecords && (
              <div className="ticket-record-empty">正在加载处理记录...</div>
            )}

            {!isLoadingRecords && ticketRecords.length === 0 && (
              <div className="ticket-record-empty">暂无处理记录</div>
            )}

            {!isLoadingRecords &&
              ticketRecords.map((record) => (
                <div className="ticket-record-item" key={record.id}>
                  {/* 左侧时间线圆点 */}
                  <div className="ticket-record-line">
                    <div className="ticket-record-dot" />
                  </div>

                  {/* 右侧内容 */}
                  <div className="ticket-record-content">
                    <div className="ticket-record-meta">
                      <div>
                        <strong>{record.createdByName}</strong>

                        <span>{record.createdByRole}</span>

                        <span>{getRecordTypeName(record.recordType)}</span>

                        {record.isInternal && (
                          <span className="ticket-internal-tag">内部</span>
                        )}
                      </div>

                      <time>{formatDateTime(record.createdAt)}</time>
                    </div>

                    <div className="ticket-record-text">{record.content}</div>

                    {/* 当前处理记录的附件 */}
                    {ticketAttachments.some(
                      (attachment) => attachment.ticketRecordId === record.id,
                    ) && (
                      <div className="ticket-record-attachments">
                        {ticketAttachments
                          .filter(
                            (attachment) =>
                              attachment.ticketRecordId === record.id,
                          )
                          .map((attachment) => (
                            <button
                              type="button"
                              className="ticket-record-attachment"
                              key={attachment.id}
                              onClick={() => {
                                downloadTicketAttachment(attachment);
                              }}
                            >
                              <span>{attachment.fileName}</span>

                              <small>
                                {formatFileSize(attachment.fileSize)}
                              </small>

                              {attachment.isInternal && (
                                <span className="ticket-internal-tag">
                                  内部
                                </span>
                              )}
                            </button>
                          ))}
                      </div>
                    )}
                  </div>
                </div>
              ))}
          </div>

          {/* =============================
    回复 / 处理记录
    ============================= */}

          {selectedTicket.status !== "Closed" && (
            <div className="ticket-reply-section">
              <h4>
                {isCustomer
                  ? "补充问题"
                  : isDeveloper
                    ? "添加处理记录"
                    : "回复 / 处理记录"}
              </h4>

              <textarea
                className="ticket-reply-textarea"
                value={recordContent}
                placeholder={
                  isCustomer
                    ? "请输入需要补充的问题信息..."
                    : "请输入回复内容或处理说明..."
                }
                onChange={(e) => {
                  setRecordContent(e.target.value);
                }}
              />

              <div className="ticket-reply-files">
                <input
                  type="file"
                  multiple
                  onChange={(e) => {
                    selectRecordFiles(e.target.files);

                    e.target.value = "";
                  }}
                />

                <span>
                  可附带截图、日志、PDF、压缩包等， 最多5个，单个最大100MB。
                </span>
              </div>

              {recordFiles.length > 0 && (
                <div className="ticket-selected-files">
                  {recordFiles.map((file, index) => (
                    <div
                      className="ticket-selected-file"
                      key={`${file.name}-${file.size}-${file.lastModified}`}
                    >
                      <div className="ticket-selected-file-info">
                        <strong>{file.name}</strong>

                        <span>{formatFileSize(file.size)}</span>
                      </div>

                      <button
                        type="button"
                        className="ticket-file-remove"
                        onClick={() => {
                          removeRecordFile(index);
                        }}
                      >
                        移除
                      </button>
                    </div>
                  ))}
                </div>
              )}

              <div className="ticket-reply-footer">
                {/* =========================
          内部记录
          =========================
          
          Customer 不显示。
          ========================= */}
                {!isCustomer && (
                  <label className="ticket-internal-checkbox">
                    <input
                      type="checkbox"
                      checked={recordIsInternal}
                      onChange={(e) => {
                        setRecordIsInternal(e.target.checked);
                      }}
                    />

                    <span>内部记录</span>

                    <small>客户不可见</small>
                  </label>
                )}

                <button
                  type="button"
                  className="primary-button"
                  disabled={isSubmittingRecord}
                  onClick={submitTicketRecord}
                >
                  {isSubmittingRecord
                    ? "正在提交..."
                    : isCustomer
                      ? "提交补充"
                      : "添加记录"}
                </button>
              </div>
            </div>
          )}

          {/* =============================
    工单状态操作
    ============================= */}

          {/* =================================
    Admin / Support / Developer
    可以标记已解决
    ================================= */}
          {(currentRole === "Admin" ||
            currentRole === "Support" ||
            currentRole === "Developer") &&
            selectedTicket.status === "Processing" && (
              <div className="ticket-action-section">
                <h4>解决工单</h4>

                <p className="ticket-action-tip">
                  确认问题已经处理完成后， 填写解决说明并标记为已解决。
                </p>

                <textarea
                  className="ticket-reply-textarea"
                  value={resolveContent}
                  placeholder="请输入问题原因、处理方法和最终解决结果..."
                  onChange={(e) => {
                    setResolveContent(e.target.value);
                  }}
                />

                <div className="ticket-action-buttons">
                  <button
                    type="button"
                    className="primary-button"
                    disabled={isChangingTicketStatus}
                    onClick={resolveTicket}
                  >
                    {isChangingTicketStatus ? "正在处理..." : "标记已解决"}
                  </button>
                </div>
              </div>
            )}
          {/* =================================
    Resolved：
    
    Customer 可以：
    - 确认解决并关闭
    - 问题仍存在重新打开

    Admin / Support：
    - 关闭
    - 重新打开
    ================================= */}
          {selectedTicket.status === "Resolved" && (
            <div className="ticket-action-section">
              <h4>{isCustomer ? "问题是否已经解决？" : "工单后续操作"}</h4>

              {isCustomer && (
                <p className="ticket-action-tip">
                  如果问题已经解决，可以关闭工单；
                  如果问题仍然存在，可以重新打开继续处理。
                </p>
              )}

              {/* Reopen 原因 */}
              {(isCustomer || canManageTicket) && (
                <textarea
                  className="ticket-reply-textarea"
                  value={reopenContent}
                  placeholder="如果问题仍然存在，请填写原因..."
                  onChange={(e) => {
                    setReopenContent(e.target.value);
                  }}
                />
              )}

              <div className="ticket-action-buttons">
                {(isCustomer || canManageTicket) && (
                  <button
                    type="button"
                    className="normal-button"
                    disabled={isChangingTicketStatus}
                    onClick={reopenTicket}
                  >
                    问题仍存在，重新打开
                  </button>
                )}

                {(isCustomer || canManageTicket) && (
                  <button
                    type="button"
                    className="primary-button"
                    disabled={isChangingTicketStatus}
                    onClick={closeTicket}
                  >
                    {isCustomer ? "确认解决并关闭" : "关闭工单"}
                  </button>
                )}
              </div>
            </div>
          )}
          {/* =================================
    Closed：
    
    Customer / Admin / Support
    如果问题再次出现，可以重新打开。
    ================================= */}
          {selectedTicket.status === "Closed" &&
            (isCustomer || canManageTicket) && (
              <div className="ticket-action-section">
                <h4>问题再次出现？</h4>

                <p className="ticket-action-tip">
                  如果该问题再次发生，可以重新打开这个工单，
                  继续保留原来的处理历史。
                </p>

                <textarea
                  className="ticket-reply-textarea"
                  value={reopenContent}
                  placeholder="请输入问题再次出现的情况..."
                  onChange={(e) => {
                    setReopenContent(e.target.value);
                  }}
                />

                <div className="ticket-action-buttons">
                  <button
                    type="button"
                    className="primary-button"
                    disabled={isChangingTicketStatus}
                    onClick={reopenTicket}
                  >
                    重新打开工单
                  </button>
                </div>
              </div>
            )}
        </div>
      )}
      {/* =============================
          搜索
          ============================= */}
      <SearchBar
        value={searchKeyword}
        placeholder="搜索工单编号、标题、客户、软件或处理人"
        onChange={setSearchKeyword}
        onClear={() => {
          setSearchKeyword("");
        }}
      />

      {/* =============================
          加载提示
          ============================= */}
      {isLoading && <div className="empty">正在加载工单...</div>}

      {/* =============================
          错误提示
          ============================= */}
      {!isLoading && errorMessage && (
        <div className="empty">{errorMessage}</div>
      )}

      {/* =============================
          工单表格
          ============================= */}
      {!isLoading && !errorMessage && filteredTickets.length > 0 && (
        <div className="ticket-table">
          {/* 表头 */}
          <div
            className={
              isCustomer
                ? "ticket-table-header ticket-table-customer"
                : "ticket-table-header"
            }
          >
            <div>工单编号</div>

            <div>标题</div>

            {!isCustomer && <div>客户</div>}

            <div>软件</div>

            <div>状态</div>

            <div>优先级</div>

            {!isCustomer && <div>提交人</div>}
            <div>处理人</div>

            <div>创建时间</div>

            <div>操作</div>
          </div>

          {/* 数据 */}
          {filteredTickets.map((ticket) => (
            <div
              className={
                isCustomer
                  ? "ticket-table-row ticket-table-customer"
                  : "ticket-table-row"
              }
              key={ticket.id}
            >
              <div title={ticket.ticketNo}>{ticket.ticketNo}</div>

              <div title={ticket.title}>{ticket.title}</div>

              {!isCustomer && (
                <div title={ticket.customerName}>{ticket.customerName}</div>
              )}

              <div title={ticket.softwareName}>{ticket.softwareName}</div>

              <div>
                <span
                  className={`ticket-status ticket-status-${ticket.status.toLowerCase()}`}
                >
                  {getStatusName(ticket.status)}
                </span>
              </div>

              <div>{getPriorityName(ticket.priority)}</div>

              {!isCustomer && <div>{ticket.createdByName}</div>}

              <div>{ticket.assignedToName ?? "未分配"}</div>

              <div>{formatDateTime(ticket.createdAt)}</div>

              <div className="table-actions">
                <button
                  type="button"
                  className="edit-button"
                  onClick={() => {
                    viewTicket(ticket);
                  }}
                >
                  查看
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* =============================
          空数据
          ============================= */}
      {!isLoading && !errorMessage && filteredTickets.length === 0 && (
        <div className="empty">暂无工单</div>
      )}
    </div>
  );
}

export default TicketPage;
