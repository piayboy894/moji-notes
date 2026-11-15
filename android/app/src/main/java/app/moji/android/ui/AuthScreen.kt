package app.moji.android.ui

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import app.moji.android.*
import app.moji.android.R
import app.moji.android.security.Envelope

@Composable fun StartupScreen(s: AppState, vm: MojiViewModel) {
    var password by remember { mutableStateOf("") }
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
        Column(Modifier.widthIn(max = 400.dp).fillMaxWidth().verticalScroll(rememberScrollState()).padding(28.dp), verticalArrangement = Arrangement.spacedBy(18.dp)) {
            Brand()
            if (s.loading) CircularProgressIndicator(Modifier.size(24.dp))
            else if (s.needsMigration) {
                Text("迁移旧版数据", style = MaterialTheme.typography.titleLarge)
                Text("输入一次旧密码，以后直接进入。", style = MaterialTheme.typography.bodyMedium)
                Field("旧密码", password, Modifier.testTag("migration-password"), secret = true) { password = it }
                Button(onClick = { vm.migrate(password) }, enabled = !s.busy && password.isNotBlank(), modifier = Modifier.testTag("migrate")) { Text("迁移并打开") }
            } else OutlinedButton(onClick = { vm.openLocal() }) { Text("重试") }
            if (s.error.isNotBlank()) Text(s.error, color = MaterialTheme.colorScheme.error)
        }
    }
}
@Composable fun Brand() { Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(12.dp)) { Image(painterResource(R.drawable.moji_icon), null, Modifier.size(39.dp)); Text("墨记", fontSize = 24.sp, fontWeight = FontWeight.SemiBold) } }
