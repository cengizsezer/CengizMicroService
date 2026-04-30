pipeline {
  agent any
  options { timestamps() }
  triggers { githubPush() }

  environment {
    COMPOSE_FILES = "-f docker-compose.yml -f docker-compose.override.yml"
  }

  stages {
    stage('Prepare secrets & .env') {
      steps {
        withCredentials([
          file(credentialsId: 'google-vision-json', variable: 'VISIONFILE'),
          string(credentialsId: 'openai-api-key',  variable: 'OPENAI_KEY'),
          string(credentialsId: 'brevo-api-key',   variable: 'BREVO_KEY')
        ]) {
          sh '''
            set -eux

            # Vision key'i workspace altına koy (repo ile birlikte mount edeceğiz)
            mkdir -p Services/OCRService/OCRService.Api/Config
            cp "$VISIONFILE" Services/OCRService/OCRService.Api/Config/vision-sa.json

            # .env dosyasını üret (sadece CI/Sunucu için)
            cat > .env <<EOF
ASPNETCORE_ENVIRONMENT=Production
OPENAI_API_KEY=${OPENAI_KEY}
GOOGLE_APPLICATION_CREDENTIALS=/app/Services/OCRService/OCRService.Api/Config/vision-sa.json
BREVO_API_KEY=${BREVO_KEY}
BREVO_FROM_ADDRESS=cengizsezer7@gmail.com
BREVO_FROM_DISPLAY_NAME=Sovos Fatura Takip
EOF

            echo "Created .env for CI with OPENAI_API_KEY, GOOGLE_APPLICATION_CREDENTIALS, BREVO_API_KEY"
          '''
        }
      }
    }

    stage('Build & Run Services') {
      steps {
        sh '''
          set -eux
          docker-compose ${COMPOSE_FILES} down || true
          docker-compose ${COMPOSE_FILES} up --build -d
        '''
      }
    }

    stage('Seed Database') {
      steps {
        sh '''
          set -eux
          docker-compose ${COMPOSE_FILES} run --rm catalogseeder.api
        '''
      }
    }
  }

  post {
    always {
      sh 'docker-compose ${COMPOSE_FILES} ps || true'
      archiveArtifacts artifacts: '.env', fingerprint: false, onlyIfSuccessful: false
    }
  }
}
