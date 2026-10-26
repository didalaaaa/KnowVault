import MarkdownIt from 'markdown-it'

// html:false 是硬性安全开关：模型输出里的原始 HTML 一律转义，避免回答注入脚本
const md = new MarkdownIt({
  html: false,
  linkify: true,
  breaks: true,
})

/**
 * 把助手回答的 Markdown 渲染成安全 HTML
 * @param {string} text Markdown 原文
 * @returns {string} 可直接交给 v-html 的 HTML
 */
export function renderMarkdown(text) {
  return md.render(text || '')
}
