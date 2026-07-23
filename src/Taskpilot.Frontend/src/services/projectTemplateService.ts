import api from '../lib/api'
import type { Project } from '../types/project'
import type { ProjectTemplate, ProjectTemplateDetail } from '../types/template'

/** REST calls for reusable project templates. */
export const projectTemplateService = {
  /** Lists the current user's templates. */
  getTemplates(): Promise<ProjectTemplate[]> {
    return api.get<ProjectTemplate[]>('/api/project-templates').then((r) => r.data)
  },

  /** Loads one template with its tasks, for previewing. */
  getTemplate(id: string): Promise<ProjectTemplateDetail> {
    return api.get<ProjectTemplateDetail>(`/api/project-templates/${id}`).then((r) => r.data)
  },

  /** Snapshots an existing project into a new template (optional custom name). */
  saveAsTemplate(projectId: string, name?: string): Promise<ProjectTemplate> {
    return api
      .post<ProjectTemplate>(`/api/projects/${projectId}/save-as-template`, { name })
      .then((r) => r.data)
  },

  /** Creates a new project from a template (optional name/color override). */
  createProjectFromTemplate(
    templateId: string,
    data: { name?: string; color?: string } = {},
  ): Promise<Project> {
    return api
      .post<Project>(`/api/project-templates/${templateId}/create-project`, data)
      .then((r) => r.data)
  },

  /** Deletes one of the current user's templates. */
  deleteTemplate(id: string): Promise<void> {
    return api.delete(`/api/project-templates/${id}`).then(() => undefined)
  },
}
