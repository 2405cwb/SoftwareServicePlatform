import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

export function createNotificationConnection() {
  return new HubConnectionBuilder()
    .withUrl("/hubs/notifications", {
      accessTokenFactory: () => sessionStorage.getItem("access_token") ?? "",
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
}
