pipeline {
  agent any
  triggers { githubPush() }   // Webhook geldiğinde tetikler

  stages {
    stage('Copy .env & Vision key from Jenkins Credentials') {
      steps {
        withCredentials([
          file(credentialsId: 'env-file',           variable: 'ENVFILE'),
          file(credentialsId: 'google-vision-json', variable: 'VISIONFILE')
        ]) {
          sh '''
            cp "$ENVFILE" .env
            mkdir -p Services/OCRService/OCRService.Api/Config
            cp "$VISIONFILE" Services/OCRService/OCRService.Api/Config/
          '''
        }
      }
    }

    stage('Build & Run Services') {
      steps {
        sh 'docker-compose -f docker-compose.yml -f docker-compose.override.yml down || true'
        sh 'docker-compose -f docker-compose.yml -f docker-compose.override.yml up --build -d'
      }
    }

    stage('Seed Database') {
      steps {
        sh 'docker-compose -f docker-compose.yml -f docker-compose.override.yml run --rm catalogseeder.api'
      }
    }
  }
}
