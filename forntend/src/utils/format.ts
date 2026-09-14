export function formatDateTime(value?: string | null) {
  if (!value) {
    return "-";
  }

  return new Date(value).toLocaleString("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function formatShortDateTime(value?: string | null) {
  if (!value) {
    return "-";
  }

  return new Date(value).toLocaleString("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function formatDuration(minutes?: number | null) {
  if (minutes === null || minutes === undefined || Number.isNaN(minutes)) {
    return "--";
  }

  if (minutes < 60) {
    return `${Math.max(0, minutes).toFixed(0)} 分钟`;
  }

  const hours = minutes / 60;
  if (hours < 24) {
    return `${hours.toFixed(1)} 小时`;
  }

  return `${(hours / 24).toFixed(1)} 天`;
}

export function formatFileSize(bytes: number) {
  if (!bytes || bytes <= 0) {
    return "0 B";
  }

  const units = ["B", "KB", "MB", "GB", "TB"];
  let value = bytes;
  let index = 0;

  while (value >= 1024 && index < units.length - 1) {
    value /= 1024;
    index++;
  }

  return `${value.toFixed(index === 0 ? 0 : 2)} ${units[index]}`;
}

export function getStatusName(status: string) {
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

export function getPriorityName(priority: string) {
  switch (priority) {
    case "Unclassified":
      return "待分诊";
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

export function getSourceName(source: string) {
  switch (source) {
    case "Portal":
      return "客户门户";
    case "WeChat":
      return "微信";
    case "Phone":
      return "电话";
    case "Email":
      return "邮件";
    case "OnSite":
      return "现场";
    case "Internal":
      return "内部录入";
    default:
      return source || "-";
  }
}
