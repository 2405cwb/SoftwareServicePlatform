/**
 * 统一的 API 请求方法。
 *
 * 作用：
 * 1. 自动读取 JWT
 * 2. 自动添加 Authorization: Bearer xxx
 * 3. 遇到 401 自动清理登录状态
 * 4. 自动跳回登录页面
 */
export async function apiFetch(
  url: string,
  options: RequestInit = {},
): Promise<Response> {
  /*
   * 登录成功后保存在 sessionStorage 中的 JWT。
   */
  const token = sessionStorage.getItem("access_token");

  /*
   * Headers 是浏览器提供的请求头对象。
   *
   * 使用 options.headers 初始化，
   * 可以保留调用方自己传进来的请求头。
   *
   * 例如：
   * Content-Type: application/json
   */
  const headers = new Headers(options.headers);

  /*
   * 如果存在 JWT，
   * 自动加到 HTTP Header。
   */
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  /*
   * 真正调用浏览器 fetch。
   */
  const response = await fetch(url, {
    ...options,

    headers,
  });

  /*
   * 401：
   *
   * Token 不存在、无效或已经过期。
   */
  if (response.status === 401) {
    /*
     * 清理登录信息。
     */
    sessionStorage.removeItem("access_token");

    sessionStorage.removeItem("current_user");

    /*
     * 强制回登录页。
     *
     * replace：
     * 不保留当前失效页面的历史记录。
     */
    window.location.replace("/login");
  }

  return response;
}
