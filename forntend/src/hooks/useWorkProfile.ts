import { useCallback, useEffect, useState } from "react";
import { apiFetch } from "../services/api";

export interface WorkProfile {
  id: number;
  username: string;
  displayName: string;
  role: string;
  customerId: number | null;
  customerName: string | null;
  openTicketCount: number;
  urgentTicketCount: number;
}

/**
 * 顶部用户信息 + 工单角标。
 *
 * 60 秒自动刷新；浏览器重新获得焦点时立即刷新。
 * 目前不需要 SignalR，也能满足内部管理平台的“及时看到待办”。
 */
export function useWorkProfile() {
  const [profile, setProfile] = useState<WorkProfile | null>(null);

  const refresh = useCallback(async () => {
    try {
      const response = await apiFetch("/api/work-dashboard/me");
      if (!response.ok) {
        return;
      }

      setProfile((await response.json()) as WorkProfile);
    } catch (error) {
      console.error("刷新个人工作信息失败：", error);
    }
  }, []);

  useEffect(() => {
    refresh();

    const timer = window.setInterval(refresh, 60_000);
    const onFocus = () => refresh();
    window.addEventListener("focus", onFocus);

    return () => {
      window.clearInterval(timer);
      window.removeEventListener("focus", onFocus);
    };
  }, [refresh]);

  return { profile, refresh };
}
