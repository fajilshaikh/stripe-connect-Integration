import { AppConfig } from "../../shared/models/config";
import { http } from "./http";

export const configService = {
  getConfig: () => http.get<AppConfig>("/config"),
};
