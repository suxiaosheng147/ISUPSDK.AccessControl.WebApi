/*
 * 门禁对接前端 —— 公共脚本
 * 后端：KrAdmin.API.Host 的 /api/acs/* 资源接口（REST 风格）
 *
 * 统一约定：
 *   - 鉴权：Authorization: bearer <token>（KrAdmin 的 AuthHandler）
 *   - 响应：OutputModel<T> = { Status, ErrType, Message, Data }
 *   - 分页：pageSize / pageIndex（pageIndex 从 1 开始），返回 PageModel<T> = { RecordCount, PageCount, PageData }
 */
(function (global) {
  'use strict';

  var CONFIG_KEY = 'acs.ui.config';
  var PAGES = [
    { href: 'index.html', text: '首页' },
    { href: 'devices.html', text: '设备' },
    { href: 'system.html', text: '服务状态' },
    { href: 'persons.html', text: '人员' },
    { href: 'cards.html', text: '卡片' },
    { href: 'faces.html', text: '人脸 / 人脸库' },
    { href: 'doors.html', text: '门禁控制' },
    { href: 'access-plans.html', text: '权限计划' },
    { href: 'events.html', text: '事件' },
    { href: 'alarms.html', text: '报警' },
    { href: 'tasks.html', text: '下发任务' },
    { href: 'storage.html', text: '图片存储' }
  ];

  /* ---------------- 配置 ---------------- */

  function config() {
    try {
      return JSON.parse(localStorage.getItem(CONFIG_KEY) || '{}') || {};
    } catch (e) {
      return {};
    }
  }

  function saveConfig(patch) {
    var next = Object.assign(config(), patch || {});
    localStorage.setItem(CONFIG_KEY, JSON.stringify(next));
    return next;
  }

  function baseUrl() {
    var value = (config().baseUrl || 'https://localhost:5001').trim();
    return value.replace(/\/+$/, '');
  }

  /* ---------------- 请求 ---------------- */

  function buildUrl(path, query) {
    var url = baseUrl() + path;
    var parts = [];
    var cfgEnv = config().environment;

    function push(key, value) {
      if (value === undefined || value === null || value === '') return;
      if (Array.isArray(value)) {
        value.forEach(function (item) { push(key, item); });
        return;
      }
      parts.push(encodeURIComponent(key) + '=' + encodeURIComponent(value));
    }

    Object.keys(query || {}).forEach(function (key) { push(key, query[key]); });

    // 除显式关闭，统一带上 environment（空则交给服务端按宿主环境判定）
    var useEnv = !query || query.__noEnv !== true;
    if (useEnv && cfgEnv) push('environment', cfgEnv);

    return parts.length ? url + (url.indexOf('?') >= 0 ? '&' : '?') + parts.join('&') : url;
  }

  /**
   * 递归把响应对象的属性名转成 camelCase。
   * KrAdmin 用 Newtonsoft 的 DefaultContractResolver（PascalCase 输出），
   * 而门禁服务原始 JSON（JToken 透传部分）本来就是 camelCase，这里统一成 camelCase 方便页面使用。
   */
  function toCamel(value) {
    if (Array.isArray(value)) return value.map(toCamel);
    if (value && typeof value === 'object') {
      var out = {};
      Object.keys(value).forEach(function (key) {
        out[key.charAt(0).toLowerCase() + key.slice(1)] = toCamel(value[key]);
      });
      return out;
    }
    return value;
  }

  /**
   * 调用后端接口。
   * @param {string} path  形如 '/api/acs/devices'
   * @param {object} opt   { method, query, body, formData, raw }
   * @returns {Promise<any>} 成功返回 Data（raw=true 时返回原始 JSON，保持 PascalCase）
   */
  async function api(path, opt) {
    opt = opt || {};
    var query = Object.assign({}, opt.query || {});
    var url = buildUrl(path, query);

    var headers = { 'Accept': 'application/json' };
    var token = (config().token || '').trim();
    if (token) headers['Authorization'] = 'bearer ' + token;

    var init = { method: (opt.method || 'GET').toUpperCase(), headers: headers };

    if (opt.formData) {
      // multipart/form-data：不设 Content-Type，交给浏览器生成 boundary
      init.body = opt.formData;
    } else if (opt.body !== undefined && opt.body !== null) {
      headers['Content-Type'] = 'application/json';
      init.body = typeof opt.body === 'string' ? opt.body : JSON.stringify(opt.body);
    }

    var response;
    try {
      response = await fetch(url, init);
    } catch (e) {
      throw new Error('无法连接后端 ' + baseUrl() + '（' + e.message + '）\n'
        + '检查：① 服务是否启动 ② API 基址是否正确 ③ 跨域是否放通（KrAdmin 的 Cors:Origins 需包含本页面来源）');
    }

    var text = await response.text();
    var json = null;
    if (text) {
      try { json = JSON.parse(text); } catch (e) { json = null; }
    }

    if (!response.ok) {
      var detail = (json && json.Message) ? json.Message : (text ? text.slice(0, 800) : response.statusText);
      throw new Error('HTTP ' + response.status + '：' + detail);
    }

    if (json && typeof json.Status === 'boolean') {
      if (opt.raw) return json;
      if (!json.Status) throw new Error(json.Message || '调用失败');
      return toCamel(json.Data);
    }

    return toCamel(json);
  }

  /**
   * 以下载二进制（抓拍图、存储文件）：接口返回 File()，需要带 token 用 fetch 取 blob
   */
  async function blob(path, query) {
    var url = buildUrl(path, query || {});
    var headers = {};
    var token = (config().token || '').trim();
    if (token) headers['Authorization'] = 'bearer ' + token;

    var response = await fetch(url, { headers: headers });
    if (!response.ok) {
      var text = await response.text();
      throw new Error('HTTP ' + response.status + '：' + (text ? text.slice(0, 400) : response.statusText));
    }

    return await response.blob();
  }

  /** 触发浏览器下载 */
  async function download(path, query, fileName) {
    var data = await blob(path, query);
    var href = URL.createObjectURL(data);
    var link = document.createElement('a');
    link.href = href;
    link.download = fileName || 'download.bin';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function () { URL.revokeObjectURL(href); }, 5000);
  }

  /* ---------------- DOM 小工具 ---------------- */
  function el(id) { return document.getElementById(id); }

  function esc(value) {
    if (value === null || value === undefined) return '';
    return String(value)
      .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
  }

  function msg(id, type, text) {
    var node = typeof id === 'string' ? el(id) : id;
    if (!node) return;
    if (!text) { node.className = 'msg hidden'; node.textContent = ''; return; }
    node.className = 'msg ' + (type || 'ok');
    node.textContent = text;
  }

  function showJson(id, value) {
    var node = el(id);
    if (!node) return;
    if (value === undefined || value === null) { node.textContent = '（无数据）'; return; }
    if (typeof value === 'string') { node.textContent = value; return; }
    try { node.textContent = JSON.stringify(value, null, 2); } catch (e) { node.textContent = String(value); }
  }

  function fmtTime(value) {
    if (!value) return '';
    var d = new Date(value);
    if (isNaN(d.getTime())) return String(value);
    var pad = function (n) { return n < 10 ? '0' + n : '' + n; };
    return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate())
      + ' ' + pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':' + pad(d.getSeconds());
  }

  /** 渲染表格：columns = [{ key, text, render(row), cls }] */
  function table(containerId, columns, rows, emptyText) {
    var host = el(containerId);
    if (!host) return;

    if (!rows || rows.length === 0) {
      host.innerHTML = '<div class="msg warn" style="margin:0">' + esc(emptyText || '暂无数据') + '</div>';
      return;
    }

    var head = columns.map(function (c) { return '<th>' + esc(c.text) + '</th>'; }).join('');
    var body = rows.map(function (row, index) {
      var cells = columns.map(function (c) {
        var value = c.render ? c.render(row, index) : esc(row[c.key]);
        var cls = c.cls ? ' class="' + c.cls + '"' : '';
        return '<td' + cls + '>' + value + '</td>';
      }).join('');
      return '<tr>' + cells + '</tr>';
    }).join('');

    host.innerHTML = '<div class="tableWrap"><table><thead><tr>' + head + '</tr></thead><tbody>' + body + '</tbody></table></div>';
  }

  /** 分页器：onChange(pageIndex) */
  function pager(containerId, pageSize, pageIndex, recordCount, pageCount, onChange) {
    var host = el(containerId);
    if (!host) return;

    host.innerHTML =
      '<button data-go="prev"' + (pageIndex <= 1 ? ' disabled' : '') + '>上一页</button>'
      + '<span class="info">第 ' + pageIndex + ' / ' + pageCount + ' 页，共 ' + recordCount + ' 条，每页 ' + pageSize + ' 条</span>'
      + '<button data-go="next"' + (pageIndex >= pageCount ? ' disabled' : '') + '>下一页</button>';

    host.querySelectorAll('button[data-go]').forEach(function (button) {
      button.addEventListener('click', function () {
        onChange(button.getAttribute('data-go') === 'prev' ? pageIndex - 1 : pageIndex + 1);
      });
    });
  }

  /** 解析 JSON 文本，失败抛出中文错误 */
  function parseJson(text, fieldName) {
    if (!text || !text.trim()) return null;
    try {
      return JSON.parse(text);
    } catch (e) {
      throw new Error((fieldName || 'JSON') + ' 格式有误：' + e.message);
    }
  }

  /** 收集同名字段：pagesize/pageindex 等从 DOM 读 */
  function queryFrom(ids) {
    var query = {};
    Object.keys(ids || {}).forEach(function (id) {
      var node = el(id);
      if (!node) return;
      var key = ids[id];
      var value = node.type === 'checkbox' ? (node.checked ? 'true' : '') : node.value;
      if (value !== '' && value !== null && value !== undefined) query[key] = value;
    });
    return query;
  }

  /** 统一的“执行并提示”包装 */
  async function run(msgId, action, okText) {
    msg(msgId, '', '');
    try {
      var result = await action();
      msg(msgId, 'ok', okText || '操作成功');
      return result;
    } catch (e) {
      msg(msgId, 'err', e.message || String(e));
      return undefined;
    }
  }

  /* ---------------- 页面骨架 ---------------- */

  function layout(options) {
    options = options || {};
    var cfg = config();

    var nav = PAGES.map(function (p) {
      var active = p.href === options.active ? ' class="active"' : '';
      return '<a href="' + p.href + '"' + active + '>' + p.text + '</a>';
    }).join('');

    var topbar = ''
      + '<div class="topbar">'
      + '  <div class="brand">'
      + '    <h1>' + esc(options.title || '门禁对接控制台') + '</h1>'
      + '    <span class="muted">KrAdmin.API · /api/acs/*（ISUP 门禁接入服务）</span>'
      + '  </div>'
      + '  <div class="cfgspacer"></div>'
      + '  <div class="cfg">'
      + '    <label class="field"><span>API 基址</span><input id="cfgBaseUrl" placeholder="https://localhost:5001" value="' + esc(cfg.baseUrl || '') + '"></label>'
      + '    <label class="field"><span>Token</span><input id="cfgToken" placeholder="登录返回的 access_token" value="' + esc(cfg.token || '') + '"></label>'
      + '    <label class="field"><span>环境</span><select id="cfgEnvironment">'
      + '      <option value="">（按服务端）</option>'
      + '      <option value="PROD">PROD</option>'
      + '      <option value="UAT">UAT</option>'
      + '      <option value="TEST">TEST</option>'
      + '      <option value="DEV">DEV</option>'
      + '    </select></label>'
      + '    <button id="cfgSave" class="primary">保存</button>'
      + '  </div>'
      + '</div>'
      + '<nav class="tabs">' + nav + '</nav>';

    var holder = el('app');
    if (holder) holder.insertAdjacentHTML('afterbegin', topbar);

    var envSelect = el('cfgEnvironment');
    if (envSelect) envSelect.value = cfg.environment || '';

    var save = el('cfgSave');
    if (save) {
      save.addEventListener('click', function () {
        saveConfig({
          baseUrl: el('cfgBaseUrl').value.trim(),
          token: el('cfgToken').value.trim(),
          environment: el('cfgEnvironment').value
        });
        save.textContent = '已保存';
        setTimeout(function () { save.textContent = '保存'; }, 1200);
      });
    }
  }

  global.ACS = {
    config: config,
    saveConfig: saveConfig,
    baseUrl: baseUrl,
    api: api,
    blob: blob,
    download: download,
    el: el,
    esc: esc,
    msg: msg,
    showJson: showJson,
    fmtTime: fmtTime,
    table: table,
    pager: pager,
    parseJson: parseJson,
    queryFrom: queryFrom,
    run: run,
    layout: layout
  };
})(window);
