export type Role = 'Admin' | 'ProjectManager' | 'TeamMember';

/** The signed-in user (backend UserDto). */
export interface CurrentUser {
  id: string;
  email: string;
  fullName: string;
  role: Role;
}

/** Body of login and refresh responses. The refresh token is never here: it is an httpOnly cookie. */
export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: CurrentUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  fullName: string;
  password: string;
}

/** Display names for roles (sidebar user row, users page). */
export const roleLabels: Record<Role, string> = {
  Admin: 'Admin',
  ProjectManager: 'Project Manager',
  TeamMember: 'Team Member',
};
