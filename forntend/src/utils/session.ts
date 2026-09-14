export type UserRole = "Admin" | "Support" | "Developer" | "Sales" | "Customer";

export interface SessionUser {
  id: number;
  username: string;
  displayName: string;
  role: UserRole;
  customerId: number | null;
  email?: string;
  phone?: string;
}

export function getSessionUser(): SessionUser | null {
  const raw = sessionStorage.getItem("current_user");
  if (!raw) {
    return null;
  }

  try {
    return JSON.parse(raw) as SessionUser;
  } catch {
    return null;
  }
}

export function getRoleName(role: string) {
  switch (role) {
    case "Admin":
      return "系统管理员";
    case "Support":
      return "售后人员";
    case "Developer":
      return "开发人员";
    case "Sales":
      return "销售人员";
    case "Customer":
      return "客户用户";
    default:
      return role;
  }
}

export function hasRole(user: SessionUser | null, ...roles: UserRole[]) {
  return !!user && roles.includes(user.role);
}
