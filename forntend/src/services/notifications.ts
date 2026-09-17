import { apiFetch } from "./api";

export interface NotificationItem {
  id: number;
  type: string;
  level: string;
  title: string;
  content: string;
  targetUrl: string | null;
  isRead: boolean;
  readAt: string | null;
  createdAt: string;
}

interface NotificationPage {
  items: NotificationItem[];
}

export async function getNotifications() {
  const response = await apiFetch("/api/notifications?page=1&pageSize=8");

  if (!response.ok) {
    throw new Error("获取通知失败");
  }

  return (await response.json()) as NotificationPage;
}

export async function getUnreadCount() {
  const response = await apiFetch("/api/notifications/unread-count");

  if (!response.ok) {
    throw new Error("获取未读数量失败");
  }

  return (await response.json()) as { count: number };
}

export async function markNotificationAsRead(id: number) {
  await apiFetch(`/api/notifications/${id}/read`, {
    method: "PUT",
  });
}

export async function markAllNotificationsAsRead() {
  await apiFetch("/api/notifications/read-all", {
    method: "PUT",
  });
}
