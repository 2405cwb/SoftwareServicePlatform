/*
 * 平台基本配置。
 */
export interface PlatformInfo {
  title: string;
  companyName: string;
}

/*
 * 获取平台公共信息。
 *
 * 这个接口允许未登录访问，
 * 所以不需要 apiFetch。
 */
export async function getPlatformInfo(): Promise<PlatformInfo> {
  const response = await fetch("/api/platform/info");

  if (!response.ok) {
    throw new Error(`获取平台配置失败：${response.status}`);
  }

  const data = (await response.json()) as PlatformInfo;

  return data;
}
